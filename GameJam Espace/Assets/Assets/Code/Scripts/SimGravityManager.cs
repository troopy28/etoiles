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
    public List<Transform> m_transforms;
    private TransformAccessArray m_transformAccessArray;
    
    public bool m_auto_update = true;
    public float G = 6.674e-11f;

    private Stack<int> m_free_id;
    
    private static readonly ProfilerMarker nBodyProfiler = new("SimGravityManager.NBody");
    private static readonly ProfilerMarker applyPositionsProfiler = new("SimGravityManager.ApplyPositions");


    void Awake()
    {
        m_curr = new NativeList<float4>(Allocator.Persistent);
        m_prev = new NativeList<float4>(Allocator.Persistent);
        m_free_id = new Stack<int>();
        m_transformAccessArray = new TransformAccessArray(0);
    }

    void OnDestroy()
    {
        m_curr.Dispose();
        m_prev.Dispose();
        m_transformAccessArray.Dispose();
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
            m_transforms.Add(null);
            m_transformAccessArray.Add(body.transform);
        }

        m_curr[id] = new float4(pos, mass);
        m_prev[id] = new float4(prev, mass);
        m_transforms[id] = body.transform;
       
        return id;
    }

    public void UnregisterBody(int id)
    {
        if (!m_curr.IsCreated) return;
        m_curr[id] = new float4(m_curr[id].xyz, 0f);
        m_prev[id] = new float4(m_prev[id].xyz, 0f);
        m_transforms[id] = null;
        m_free_id.Push(id);
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
                G_dt2 = G * delta_time_sq
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
        [ReadOnly] public float G_dt2;

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

            // Write new position into prev (double buffer, swapped after job)
            p_prev[i_current_body] = new float4(2 * pos - old_pos + acc * G_dt2, cur.w);
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
