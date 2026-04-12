using UnityEngine;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

public class CSimGravityManager : MonoBehaviour
{
    public NativeList<float3> m_curr_pos;
    public NativeList<float3> m_prev_pos;
    public NativeList<float> m_mass;
    public bool m_auto_update = true;
    public float G = 6.674e-11f;

    private Stack<int> m_free_id;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        m_curr_pos = new NativeList<float3>(Allocator.Persistent);
        m_prev_pos = new NativeList<float3>(Allocator.Persistent);
        m_mass = new NativeList<float>(Allocator.Persistent);
        m_free_id = new Stack<int>();
    }

    void OnDestroy()
    {
        m_curr_pos.Dispose();
        m_prev_pos.Dispose();
        m_mass.Dispose();
    }

    public int RegisterBody(float3 pos, float3 initial_velocity, float mass)
    {
        int id = 0;
        if (m_free_id.Count > 0)
            id = m_free_id.Pop();
        else
        {
            id = m_curr_pos.Length;
            m_curr_pos.Add(default);
            m_prev_pos.Add(default);
            m_mass.Add(default);
        }

        m_curr_pos[id] = pos;
        m_prev_pos[id] = pos - initial_velocity * Time.fixedDeltaTime;
        m_mass[id] = mass;
        return id;
    }

    public void UnregisterBody(int id)
    {
        if (!m_mass.IsCreated) return;
        m_mass[id] = 0f;
        m_free_id.Push(id);
    }

    // Update is called once per frame
    void Update()
    {

    }

    void SimTick(float delta_time)
    {
        float delta_time_sq = delta_time * delta_time;
        var job = new NBodyVerletJob
        {
            curr_pos = m_curr_pos.AsArray(),
            prev_pos = m_prev_pos.AsArray(),
            mass = m_mass.AsArray(),
            G = G,
            delta_time_sq = delta_time_sq
        };
        job.Schedule(m_curr_pos.Length, 64).Complete();
        (m_curr_pos, m_prev_pos) = (m_prev_pos, m_curr_pos);
    }

    // FixedUpdate is called at a fixed interval
    void FixedUpdate()
    {
        if(m_auto_update)
        {
            SimTick(Time.fixedDeltaTime);
        }
    }

    // Burst-compiled job for calculating gravitational forces
    // then applying them to update positions with Verlet integration
    // <!> prev_pos is used as write buffer, curr_pos is read-only. REMEMBER TO SWAP THEM AFTER Complete()!
    [BurstCompile]
    public struct NBodyVerletJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> curr_pos;
        // prev_pos is used as write buffer: the job writes new positions here,
        // then curr_pos and prev_pos are swapped after Complete()
        public NativeArray<float3> prev_pos;
        [ReadOnly] public NativeArray<float> mass;
        [ReadOnly] public float G;
        [ReadOnly] public float delta_time_sq;

        public void Execute(int i_current_body)
        {
            float3 acc = float3.zero;
            float3 pos = curr_pos[i_current_body];
            float3 old_pos = prev_pos[i_current_body];
            for (int i = 0; i < curr_pos.Length; i++)
            {
                if (i == i_current_body) continue;
                float3 dir = curr_pos[i] - pos;
                float dist_sq = math.lengthsq(dir) + 1e-10f;
                acc += mass[i] * dir / (dist_sq * math.sqrt(dist_sq)); // /d^3 because dir is not normalized
            }

            // Write new position into prev_pos (double buffer, swapped after job)
            prev_pos[i_current_body] = 2 * pos - old_pos + acc * G * delta_time_sq;
        }
    }
}
