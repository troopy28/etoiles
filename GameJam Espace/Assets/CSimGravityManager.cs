using UnityEngine;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

public class CSimGravityManager : MonoBehaviour
{
    // float4: xyz = position, w = mass
    public NativeList<float4> m_curr;
    public NativeList<float4> m_prev;
    public bool m_auto_update = true;
    public float G = 6.674e-11f;

    private Stack<int> m_free_id;

    void Awake()
    {
        m_curr = new NativeList<float4>(Allocator.Persistent);
        m_prev = new NativeList<float4>(Allocator.Persistent);
        m_free_id = new Stack<int>();
    }

    void OnDestroy()
    {
        m_curr.Dispose();
        m_prev.Dispose();
    }

    public int RegisterBody(float3 pos, float3 initial_velocity, float mass)
    {
        int id;
        float3 prev = pos - initial_velocity * Time.fixedDeltaTime;
        if (m_free_id.Count > 0)
            id = m_free_id.Pop();
        else
        {
            id = m_curr.Length;
            m_curr.Add(default);
            m_prev.Add(default);
        }

        m_curr[id] = new float4(pos, mass);
        m_prev[id] = new float4(prev, mass);
        return id;
    }

    public void UnregisterBody(int id)
    {
        if (!m_curr.IsCreated) return;
        m_curr[id] = new float4(m_curr[id].xyz, 0f);
        m_prev[id] = new float4(m_prev[id].xyz, 0f);
        m_free_id.Push(id);
    }

    public float3 GetPosition(int id)
    {
        return m_curr[id].xyz;
    }

    void Update()
    {

    }

    void SimTick(float delta_time)
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

    void FixedUpdate()
    {
        if(m_auto_update)
        {
            SimTick(Time.fixedDeltaTime);
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

        public void Execute(int i_current_body)
        {
            float4 cur = curr[i_current_body];
            float3 pos = cur.xyz;
            float3 old_pos = prev[i_current_body].xyz;
            float3 acc = float3.zero;

            // No branch to skip self-interaction: when i == i_current_body,
            // dir = 0 so mass * dir * inv_dist3 = 0 (0 * finite = 0 in IEEE 754).
            for (int i = 0; i < curr.Length; i++)
            {
                float4 other = curr[i];
                float m = other.w;
                float3 dir = other.xyz - pos;
                float dist_sq = math.lengthsq(dir) + 1e-10f;
                float inv_dist = math.rsqrt(dist_sq);
                float inv_dist3 = inv_dist * inv_dist * inv_dist;
                acc += m * dir * inv_dist3;
            }

            // Write new position into prev (double buffer, swapped after job)
            prev[i_current_body] = new float4(2 * pos - old_pos + acc * G_dt2, cur.w);
        }
    }
}
