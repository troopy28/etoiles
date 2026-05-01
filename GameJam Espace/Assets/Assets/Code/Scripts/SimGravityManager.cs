using UnityEngine;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine.Jobs;
using Assets.Code.Scripts.Generation;

public class SimGravityManager : MonoBehaviour
{
    // double4: xyz = position (double precision, survives 600k+ unit universes), w = mass.
    // Float32 lost precision around |coord| ~ 50k for typical orbital deltas; double is safe to ~10^15.
    public NativeList<double4> m_curr;
    public NativeList<double4> m_prev;
    public NativeList<float3> m_external_acc;   // per-body external acceleration (thrust); kept float (input from gameplay)
    public NativeList<int>    m_stellar_class;
    public NativeList<int>    m_body_kind;      // BodyKind enum cast to int, parallel to m_curr
    public List<Transform> m_transforms;
    private TransformAccessArray m_transformAccessArray;

    public bool m_auto_update = true;
    public float G = 6.674e-11f;

    private Stack<int> m_free_id;
    private Transform m_dummy_transform;   // placeholder for freed slots so TransformAccessArray never has nulls

    private static readonly ProfilerMarker nBodyProfiler = new("SimGravityManager.NBody");
    private static readonly ProfilerMarker applyPositionsProfiler = new("SimGravityManager.ApplyPositions");
    private static readonly ProfilerMarker findRefuelStarProfiler = new("SimGravityManager.FindClosestRefuelStar");


    void Awake()
    {
        m_curr = new NativeList<double4>(Allocator.Persistent);
        m_prev = new NativeList<double4>(Allocator.Persistent);
        m_external_acc = new NativeList<float3>(Allocator.Persistent);
        m_free_id = new Stack<int>();
        m_transformAccessArray = new TransformAccessArray(0);
        m_stellar_class = new NativeList<int>(Allocator.Persistent);
        m_body_kind = new NativeList<int>(Allocator.Persistent);

        var dummy = new GameObject("__GravityDummy__");
        dummy.hideFlags = HideFlags.HideAndDontSave;
        m_dummy_transform = dummy.transform;
    }

    void OnDestroy()
    {
        m_curr.Dispose();
        m_prev.Dispose();
        m_external_acc.Dispose();
        m_transformAccessArray.Dispose();
        m_stellar_class.Dispose();
        m_body_kind.Dispose();
        if (m_dummy_transform != null) Destroy(m_dummy_transform.gameObject);
    }

    public int RegisterBody(SimGravityBody body)
    {
        double3 pos = (double3)(float3)body.transform.position;
        double3 initialVelocity = (double3)body.m_initial_velocity;
        double mass = body.mass;

        int id;
        double3 prev = pos - initialVelocity * Time.fixedDeltaTime;
        if (m_free_id.Count > 0)
        {
            id = m_free_id.Pop();

            // Place the new transform first so the rebuilt TransformAccessArray
            // sees no null at this index; other freed slots still hold the dummy.
            m_transforms[id] = body.transform;
            m_stellar_class[id] = (int)body.spectral_class;
            m_body_kind[id] = (int)body.kind;

            m_transformAccessArray.Dispose();
            m_transformAccessArray = new TransformAccessArray(m_transforms.ToArray());
        }
        else
        {
            id = m_curr.Length;
            m_curr.Add(default);
            m_prev.Add(default);
            m_external_acc.Add(float3.zero);
            m_transforms.Add(body.transform);
            m_transformAccessArray.Add(body.transform);
            m_stellar_class.Add((int)body.spectral_class);
            m_body_kind.Add((int)body.kind);
        }

        m_curr[id] = new double4(pos, mass);
        m_prev[id] = new double4(prev, mass);
        m_external_acc[id] = float3.zero;

        return id;
    }

    public void UnregisterBody(int id)
    {
        if (!m_curr.IsCreated) return;
        m_curr[id] = new double4(m_curr[id].xyz, 0.0);
        m_prev[id] = new double4(m_prev[id].xyz, 0.0);
        m_external_acc[id] = float3.zero;
        m_transforms[id] = m_dummy_transform;
        m_stellar_class[id] = 0;
        m_body_kind[id] = (int)BodyKind.Other;
        m_free_id.Push(id);
    }

    public void SetExternalAcceleration(int id, float3 acc)
    {
        if (!m_external_acc.IsCreated || id < 0 || id >= m_external_acc.Length) return;
        m_external_acc[id] = acc;
    }

    // Implicit Verlet velocity: (curr - prev) / fixedDt. Returned as float3 since the magnitudes
    // are normal in-system speeds (m/s), not mega-coordinates.
    public float3 GetVelocity(int id)
    {
        if (!m_curr.IsCreated || id < 0 || id >= m_curr.Length) return float3.zero;
        double3 v = (m_curr[id].xyz - m_prev[id].xyz) / Time.fixedDeltaTime;
        return (float3)v;
    }

    // Zeroes the implicit Verlet velocity (prev ← curr). Useful for emergency stop.
    public void ZeroVelocity(int id)
    {
        if (!m_curr.IsCreated || id < 0 || id >= m_curr.Length) return;
        double4 c = m_curr[id];
        m_prev[id] = new double4(c.xyz, m_prev[id].w);
    }

    // Returns the body's world position (cast double→float at boundary).
    public Vector3 GetPosition(int id)
    {
        if (!m_curr.IsCreated || id < 0 || id >= m_curr.Length) return Vector3.zero;
        return (Vector3)(float3)m_curr[id].xyz;
    }

    // Predicts the gravity-only trajectory of body `target_id` for `samples` future
    // steps of `dt` seconds. Returns float3 positions (caller-friendly), but integrates in double.
    public NativeArray<float3> PredictTrajectory(int target_id, float dt, int samples, Allocator allocator)
    {
        var result = new NativeArray<float3>(samples > 0 ? samples : 0, allocator);
        if (!m_curr.IsCreated || target_id < 0 || target_id >= m_curr.Length || samples <= 0)
            return result;

        int n = m_curr.Length;
        var sim_curr = new NativeArray<double4>(n, Allocator.TempJob);
        var sim_prev = new NativeArray<double4>(n, Allocator.TempJob);
        var zero_acc = new NativeArray<float3>(n, Allocator.TempJob);

        NativeArray<double4>.Copy(m_curr.AsArray(), sim_curr);
        NativeArray<double4>.Copy(m_prev.AsArray(), sim_prev);

        // Live verlet velocity is implicit over fixedDt: (curr - prev) / fixedDt.
        // Rescale prev so that (curr - prev) corresponds to velocity * dt instead.
        double scale = (double)dt / Time.fixedDeltaTime;
        if (math.abs(scale - 1.0) > 1e-9)
        {
            for (int i = 0; i < n; i++)
            {
                double3 disp = sim_curr[i].xyz - sim_prev[i].xyz;
                sim_prev[i] = new double4(sim_curr[i].xyz - disp * scale, sim_prev[i].w);
            }
        }

        double dt2 = (double)dt * dt;
        double G_dt2 = (double)G * dt2;

        for (int s = 0; s < samples; s++)
        {
            var job = new NBodyVerletJob
            {
                curr = sim_curr,
                prev = sim_prev,
                external_acc = zero_acc,
                G_dt2 = G_dt2,
                dt2 = dt2
            };
            job.Schedule(n, 64).Complete();
            (sim_curr, sim_prev) = (sim_prev, sim_curr);
            result[s] = (float3)sim_curr[target_id].xyz;
        }

        sim_curr.Dispose();
        sim_prev.Dispose();
        zero_acc.Dispose();

        return result;
    }

    void SimTick(float delta_time)
    {
        using (nBodyProfiler.Auto())
        {
            double dt = delta_time;
            double dt2 = dt * dt;
            var job = new NBodyVerletJob
            {
                curr = m_curr.AsArray(),
                prev = m_prev.AsArray(),
                external_acc = m_external_acc.AsArray(),
                G_dt2 = (double)G * dt2,
                dt2 = dt2
            };
            job.Schedule(m_curr.Length, 64).Complete();
            (m_curr, m_prev) = (m_prev, m_curr);
        }
    }

    void UpdatePositions()
    {
        using (applyPositionsProfiler.Auto())
        {
            var job = new ApplyPositionsJob
            {
                Positions = m_curr.AsArray()
            };
            job.Schedule(m_transformAccessArray).Complete();
        }
    }

    void FixedUpdate()
    {
        if(m_auto_update)
        {
            SimTick(Time.fixedDeltaTime);
            UpdatePositions();
        }
    }

    // Burst-compiled Verlet integrator, double precision.
    // double4 layout: xyz = position, w = mass. prev is read+write (write target after compute).
    // FloatMode.Default keeps IEEE-754 strictness (the whole point of switching to double).
    [BurstCompile]
    public struct NBodyVerletJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<double4> curr;
        public NativeArray<double4> prev;
        [ReadOnly] public NativeArray<float3> external_acc;
        [ReadOnly] public double G_dt2;
        [ReadOnly] public double dt2;

        public unsafe void Execute(int i_current_body)
        {
            double4* p_curr = (double4*)curr.GetUnsafeReadOnlyPtr();
            double4* p_prev = (double4*)prev.GetUnsafePtr();

            double4 cur = p_curr[i_current_body];
            double3 pos = cur.xyz;
            double3 old_pos = p_prev[i_current_body].xyz;

            // 4 independent accumulators to break loop-carried dependency.
            double3 acc0 = double3.zero;
            double3 acc1 = double3.zero;
            double3 acc2 = double3.zero;
            double3 acc3 = double3.zero;

            // No branch to skip self-interaction: dir = 0 → m * dir * inv_dist3 = 0.
            int len = curr.Length;
            double4* ptr = p_curr;
            double4* end4 = p_curr + (len & ~3);
            double4* end = p_curr + len;

            for (; ptr < end4; ptr += 4)
            {
                double4 o0 = ptr[0];
                double m0 = o0.w;
                double3 d0 = o0.xyz - pos;
                double ds0 = math.lengthsq(d0) + 1e-20;
                double id0 = math.rsqrt(ds0);
                id0 = id0 * id0 * id0;
                acc0 += m0 * d0 * id0;

                double4 o1 = ptr[1];
                double m1 = o1.w;
                double3 d1 = o1.xyz - pos;
                double ds1 = math.lengthsq(d1) + 1e-20;
                double id1 = math.rsqrt(ds1);
                id1 = id1 * id1 * id1;
                acc1 += m1 * d1 * id1;

                double4 o2 = ptr[2];
                double m2 = o2.w;
                double3 d2 = o2.xyz - pos;
                double ds2 = math.lengthsq(d2) + 1e-20;
                double id2 = math.rsqrt(ds2);
                id2 = id2 * id2 * id2;
                acc2 += m2 * d2 * id2;

                double4 o3 = ptr[3];
                double m3 = o3.w;
                double3 d3 = o3.xyz - pos;
                double ds3 = math.lengthsq(d3) + 1e-20;
                double id3 = math.rsqrt(ds3);
                id3 = id3 * id3 * id3;
                acc3 += m3 * d3 * id3;
            }
            for (; ptr < end; ptr++)
            {
                double4 other = *ptr;
                double m = other.w;
                double3 dir = other.xyz - pos;
                double dist_sq = math.lengthsq(dir) + 1e-20;
                double inv_dist = math.rsqrt(dist_sq);
                inv_dist = inv_dist * inv_dist * inv_dist;
                acc0 += m * dir * inv_dist;
            }
            double3 acc = acc0 + acc1 + acc2 + acc3;

            // Verlet update: new_pos = 2*pos - old_pos + acc*G*dt² + ext_acc*dt²
            double3 ext = (double3)external_acc[i_current_body];
            p_prev[i_current_body] = new double4(
                2.0 * pos - old_pos + acc * G_dt2 + ext * dt2,
                cur.w);
        }
    }

    [BurstCompile]
    public struct ApplyPositionsJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<double4> Positions;

        public void Execute(int index, TransformAccess transform)
        {
            double4 p = Positions[index];
            if (p.w == 0.0)
                return;
            // Cast double→float at the GPU/render boundary (Unity transforms are float).
            transform.position = new Vector3((float)p.x, (float)p.y, (float)p.z);
        }
    }

    // Finds the nearest star whose spectral class allows refueling (O, B, A, F).
    public bool TryFindClosestRefuelStar(float3 from, out int out_id, out float out_distance)
    {
        out_id = -1;
        out_distance = float.PositiveInfinity;
        if (!m_curr.IsCreated || m_curr.Length == 0) return false;

        using (findRefuelStarProfiler.Auto())
        {
            var result_id = new NativeArray<int>(1, Allocator.TempJob);
            var result_dist_sq = new NativeArray<double>(1, Allocator.TempJob);
            result_id[0] = -1;
            result_dist_sq[0] = double.PositiveInfinity;

            var job = new FindClosestRefuelStarJob
            {
                positions = m_curr.AsArray(),
                body_kinds = m_body_kind.AsArray(),
                stellar_classes = m_stellar_class.AsArray(),
                from_position = (double3)from,
                star_kind = (int)BodyKind.Star,
                max_class = (int)StellarClass.F,
                out_id = result_id,
                out_dist_sq = result_dist_sq
            };
            job.Schedule().Complete();

            int id = result_id[0];
            double dist_sq = result_dist_sq[0];
            result_id.Dispose();
            result_dist_sq.Dispose();

            if (id < 0) return false;
            out_id = id;
            out_distance = (float)math.sqrt(dist_sq);
            return true;
        }
    }

    [BurstCompile]
    public struct FindClosestRefuelStarJob : IJob
    {
        [ReadOnly] public NativeArray<double4> positions;
        [ReadOnly] public NativeArray<int> body_kinds;
        [ReadOnly] public NativeArray<int> stellar_classes;
        public double3 from_position;
        public int star_kind;
        public int max_class;
        public NativeArray<int> out_id;
        public NativeArray<double> out_dist_sq;

        public unsafe void Execute()
        {
            int n = positions.Length;
            double4* p_pos = (double4*)positions.GetUnsafeReadOnlyPtr();
            int* p_kind = (int*)body_kinds.GetUnsafeReadOnlyPtr();
            int* p_class = (int*)stellar_classes.GetUnsafeReadOnlyPtr();

            int best_id = -1;
            double best_dist_sq = double.PositiveInfinity;

            for (int i = 0; i < n; i++)
            {
                if (p_kind[i] != star_kind) continue;
                if (p_class[i] > max_class) continue;
                double3 d = p_pos[i].xyz - from_position;
                double dist_sq = math.lengthsq(d);
                if (dist_sq < best_dist_sq)
                {
                    best_dist_sq = dist_sq;
                    best_id = i;
                }
            }

            out_id[0] = best_id;
            out_dist_sq[0] = best_dist_sq;
        }
    }
}
