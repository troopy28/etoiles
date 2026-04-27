using UnityEngine;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine.Jobs;

public class SimGravityManager : MonoBehaviour
{
    // float4: xyz = position, w = mass
    public NativeList<float4> m_curr;
    public NativeList<float4> m_prev;
    public NativeList<float3> m_external_acc;   // per-body external acceleration (thrust, etc.)
    public List<Transform> m_transforms;
    private TransformAccessArray m_transformAccessArray;
    
    public bool m_auto_update = true;
    public float G = 6.674e-11f;

    private Stack<int> m_free_id;
    private Transform m_dummy_transform;   // placeholder for freed slots so TransformAccessArray never has nulls

    private static readonly ProfilerMarker nBodyProfiler = new("SimGravityManager.NBody");
    private static readonly ProfilerMarker applyPositionsProfiler = new("SimGravityManager.ApplyPositions");


    void Awake()
    {
        m_curr = new NativeList<float4>(Allocator.Persistent);
        m_prev = new NativeList<float4>(Allocator.Persistent);
        m_external_acc = new NativeList<float3>(Allocator.Persistent);
        m_free_id = new Stack<int>();
        m_transformAccessArray = new TransformAccessArray(0);

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
        if (m_dummy_transform != null) Destroy(m_dummy_transform.gameObject);
    }

    public int RegisterBody(SimGravityBody body)
    {
        float3 pos = body.transform.position;
        float3 initialVelocity = body.m_initial_velocity;
        float mass = body.mass;
        
        int id;
        float3 prev = pos - initialVelocity * Time.fixedDeltaTime;
        if (m_free_id.Count > 0)
        {
            id = m_free_id.Pop();

            // Place the new transform first so the rebuilt TransformAccessArray
            // sees no null at this index; other freed slots still hold the dummy.
            m_transforms[id] = body.transform;

            // Slot réutilisé : on ne peut pas "remplacer" un index arbitraire
            // => on est obligé de rebuild dans ce cas uniquement.
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
        }

        m_curr[id] = new float4(pos, mass);
        m_prev[id] = new float4(prev, mass);
        m_external_acc[id] = float3.zero;

        return id;
    }

    public void UnregisterBody(int id)
    {
        if (!m_curr.IsCreated) return;
        m_curr[id] = new float4(m_curr[id].xyz, 0f);
        m_prev[id] = new float4(m_prev[id].xyz, 0f);
        m_external_acc[id] = float3.zero;
        // Replace the destroyed transform with the dummy so the next rebuild has no nulls.
        m_transforms[id] = m_dummy_transform;
        m_free_id.Push(id);
    }

    public void SetExternalAcceleration(int id, float3 acc)
    {
        if (!m_external_acc.IsCreated || id < 0 || id >= m_external_acc.Length) return;
        m_external_acc[id] = acc;
    }

    // Implicit Verlet velocity: (curr - prev) / fixedDt.
    public float3 GetVelocity(int id)
    {
        if (!m_curr.IsCreated || id < 0 || id >= m_curr.Length) return float3.zero;
        return (m_curr[id].xyz - m_prev[id].xyz) / Time.fixedDeltaTime;
    }

    // Zeroes the implicit Verlet velocity (prev ← curr). Useful for emergency stop.
    public void ZeroVelocity(int id)
    {
        if (!m_curr.IsCreated || id < 0 || id >= m_curr.Length) return;
        float4 c = m_curr[id];
        m_prev[id] = new float4(c.xyz, m_prev[id].w);
    }

    // Predicts the gravity-only trajectory of body `target_id` for `samples` future
    // steps of `dt` seconds. Other bodies are also propagated (so the prediction
    // accounts for their motion). External thrust is NOT applied during prediction
    // (free-fall trajectory). Returns positions at t+dt, t+2dt, ..., t+samples*dt.
    // Caller owns the returned NativeArray and must Dispose() it.
    public NativeArray<float3> PredictTrajectory(int target_id, float dt, int samples, Allocator allocator)
    {
        var result = new NativeArray<float3>(samples > 0 ? samples : 0, allocator);
        if (!m_curr.IsCreated || target_id < 0 || target_id >= m_curr.Length || samples <= 0)
            return result;

        int n = m_curr.Length;
        var sim_curr = new NativeArray<float4>(n, Allocator.TempJob);
        var sim_prev = new NativeArray<float4>(n, Allocator.TempJob);
        var zero_acc = new NativeArray<float3>(n, Allocator.TempJob);   // gravity-only

        NativeArray<float4>.Copy(m_curr.AsArray(), sim_curr);
        NativeArray<float4>.Copy(m_prev.AsArray(), sim_prev);

        // Live verlet velocity is implicit over fixedDt: (curr - prev) / fixedDt.
        // Rescale prev so that (curr - prev) corresponds to velocity * dt instead,
        // otherwise the first prediction step would integrate over the wrong interval.
        float scale = dt / Time.fixedDeltaTime;
        if (math.abs(scale - 1f) > 1e-6f)
        {
            for (int i = 0; i < n; i++)
            {
                float3 disp = sim_curr[i].xyz - sim_prev[i].xyz;
                sim_prev[i] = new float4(sim_curr[i].xyz - disp * scale, sim_prev[i].w);
            }
        }

        float dt2 = dt * dt;
        float G_dt2 = G * dt2;

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
            result[s] = sim_curr[target_id].xyz;
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
            float delta_time_sq = delta_time * delta_time;
            var job = new NBodyVerletJob
            {
                curr = m_curr.AsArray(),
                prev = m_prev.AsArray(),
                external_acc = m_external_acc.AsArray(),
                G_dt2 = G * delta_time_sq,
                dt2 = delta_time_sq
            };
            job.Schedule(m_curr.Length, 64).Complete();
            (m_curr, m_prev) = (m_prev, m_curr);
        }
    }

    void UpdatePositions()
    {
        // Applies the computed positions to each game-object
        // simulated by the system.
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

    // Burst-compiled job for calculating gravitational forces
    // then applying them to update positions with Verlet integration
    // <!> prev is used as write buffer, curr is read-only. REMEMBER TO SWAP THEM AFTER Complete()!
    // float4 layout: xyz = position, w = mass (single array = single cache stream)
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    public struct NBodyVerletJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float4> curr;
        // prev is used as write buffer: the job writes new positions here,
        // then curr and prev are swapped after Complete()
        public NativeArray<float4> prev;
        [ReadOnly] public NativeArray<float3> external_acc;
        [ReadOnly] public float G_dt2;
        [ReadOnly] public float dt2;

        // Fast inverse square root using SSE if available, otherwise fallback to math.rsqrt
        // math.rsqrt does a Newton-Raphson iteration for better accuracy, which is unnecessary for our use case and much slower.
        static float fast_rsqrt(float x)
        {
            if (X86.Sse.IsSseSupported)
                return X86.Sse.rsqrt_ss(new v128(x)).Float0;
            return math.rsqrt(x);
        }

        // <unsafe>: use raw pointers instead of NativeArray indexing so Burst generates
        // constant-offset loads (ptr[0], ptr[1], ...) instead of recomputing
        // base + index * 16 for each body (~12 address instructions saved per unrolled iteration).
        // Safe in practice: NativeArray memory is contiguous and valid for the job's lifetime.
        public unsafe void Execute(int i_current_body)
        {
            float4* p_curr = (float4*)curr.GetUnsafeReadOnlyPtr();
            float4* p_prev = (float4*)prev.GetUnsafePtr();

            float4 cur = p_curr[i_current_body];
            float3 pos = cur.xyz;
            float3 old_pos = p_prev[i_current_body].xyz;

            // 4 independent accumulators to break loop-carried dependency,
            // allowing CPU out-of-order execution to interleave 4 chains.
            float3 acc0 = float3.zero;
            float3 acc1 = float3.zero;
            float3 acc2 = float3.zero;
            float3 acc3 = float3.zero;

            // No branch to skip self-interaction: when i == i_current_body,
            // dir = 0 so m * dir * inv_dist3 = 0 (0 * finite = 0 in IEEE 754).
            int len = curr.Length;
            float4* ptr = p_curr;
            float4* end4 = p_curr + (len & ~3);
            float4* end = p_curr + len;

            for (; ptr < end4; ptr += 4)
            {
                float4 o0 = ptr[0];
                float m0 = o0.w;
                float3 d0 = o0.xyz - pos;
                float ds0 = math.lengthsq(d0) + 1e-10f;
                float id0 = fast_rsqrt(ds0);
                id0 = id0 * id0 * id0;
                acc0 += m0 * d0 * id0;

                float4 o1 = ptr[1];
                float m1 = o1.w;
                float3 d1 = o1.xyz - pos;
                float ds1 = math.lengthsq(d1) + 1e-10f;
                float id1 = fast_rsqrt(ds1);
                id1 = id1 * id1 * id1;
                acc1 += m1 * d1 * id1;

                float4 o2 = ptr[2];
                float m2 = o2.w;
                float3 d2 = o2.xyz - pos;
                float ds2 = math.lengthsq(d2) + 1e-10f;
                float id2 = fast_rsqrt(ds2);
                id2 = id2 * id2 * id2;
                acc2 += m2 * d2 * id2;

                float4 o3 = ptr[3];
                float m3 = o3.w;
                float3 d3 = o3.xyz - pos;
                float ds3 = math.lengthsq(d3) + 1e-10f;
                float id3 = fast_rsqrt(ds3);
                id3 = id3 * id3 * id3;
                acc3 += m3 * d3 * id3;
            }
            for (; ptr < end; ptr++)
            {
                float4 other = *ptr;
                float m = other.w;
                float3 dir = other.xyz - pos;
                float dist_sq = math.lengthsq(dir) + 1e-10f;
                float inv_dist = fast_rsqrt(dist_sq);
                inv_dist = inv_dist * inv_dist * inv_dist;
                acc0 += m * dir * inv_dist;
            }
            float3 acc = acc0 + acc1 + acc2 + acc3;

            // Write new position into prev (double buffer, swapped after job).
            // Gravity displacement = acc * G * dt², external displacement = ext_acc * dt².
            p_prev[i_current_body] = new float4(
                2 * pos - old_pos + acc * G_dt2 + external_acc[i_current_body] * dt2,
                cur.w);
        }
    }
    
    [BurstCompile]
    public struct ApplyPositionsJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float4> Positions;

        public void Execute(int index, TransformAccess transform)
        {
            float4 p = Positions[index];
            if (p.w == 0f)
                return;
            transform.position = (Vector3)p.xyz;
        }
    }
}
