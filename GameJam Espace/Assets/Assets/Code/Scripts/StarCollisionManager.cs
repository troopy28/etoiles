using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

// Detects star-star collisions (any pair of StarMarker-tagged objects whose bounding
// spheres overlap) and triggers a supernova at the contact point. Both stars are
// destroyed; their SimGravityBody.OnDestroy unregisters them from the gravity sim.
//
// Pairwise distance test is offloaded to a Burst-compiled IJobParallelFor (the
// outer index i is parallelized; the inner loop iterates j > i). Detected pairs
// are written to a shared NativeQueue and resolved on the main thread.
public class StarCollisionManager : MonoBehaviour
{
    [Header("Collision")]
    public float m_collision_radius_multiplier = 1.0f;   // tweak for more permissive contact

    [Header("Supernova")]
    public float m_supernova_size_multiplier = 6f;       // peak radius = (r_a + r_b) * this
    public float m_supernova_duration = 2.5f;
    public Material m_supernova_material;                // optional override; built from shader if null

    private Material m_runtime_material;

    // Persistent native buffers reused across ticks (avoids per-frame allocation churn).
    private NativeList<float4> m_star_data;       // xyz = pos, w = collision radius
    private NativeQueue<int2> m_collisions;
    private bool m_native_allocated = false;

    // Managed list mirroring m_star_data indices, used to resolve job results back to StarMarkers.
    private readonly List<StarMarker> m_star_snapshot = new List<StarMarker>(64);

    void Awake()
    {
        if (m_supernova_material != null)
        {
            m_runtime_material = m_supernova_material;
        }
        else
        {
            Shader sh = Shader.Find("Custom/Supernova");
            if (sh != null)
                m_runtime_material = new Material(sh);
            else
                Debug.LogWarning("[StarCollisionManager] Shader 'Custom/Supernova' not found. " +
                                 "Add it to Project Settings → Graphics → Always Included Shaders.");
        }

        m_star_data = new NativeList<float4>(64, Allocator.Persistent);
        m_collisions = new NativeQueue<int2>(Allocator.Persistent);
        m_native_allocated = true;
    }

    void OnDestroy()
    {
        if (m_native_allocated)
        {
            m_star_data.Dispose();
            m_collisions.Dispose();
            m_native_allocated = false;
        }
    }

    void FixedUpdate()
    {
        var stars = StarMarker.AllStars;
        int n = stars.Count;
        if (n < 2) return;

        // Snapshot positions/radii into native buffer, in lockstep with m_star_snapshot.
        m_star_data.Clear();
        m_star_snapshot.Clear();
        float r_mult = m_collision_radius_multiplier;
        for (int i = 0; i < n; i++)
        {
            var s = stars[i];
            if (s == null) continue;
            float r = s.transform.localScale.x * 0.5f * r_mult;
            m_star_data.Add(new float4((float3)s.transform.position, r));
            m_star_snapshot.Add(s);
        }

        int count = m_star_data.Length;
        if (count < 2) return;

        m_collisions.Clear();
        var job = new StarCollisionJob
        {
            stars = m_star_data.AsArray(),
            collisions = m_collisions.AsParallelWriter()
        };
        // Outer index parallelized in batches of 8. Inner loop runs sequentially per index.
        job.Schedule(count, 8).Complete();

        if (m_collisions.Count == 0) return;

        // Resolve at most one collision per star this tick (multi-body pile-ups handled across ticks).
        var consumed = new NativeArray<bool>(count, Allocator.Temp);
        while (m_collisions.TryDequeue(out int2 pair))
        {
            if (consumed[pair.x] || consumed[pair.y]) continue;
            consumed[pair.x] = true;
            consumed[pair.y] = true;
            Collide(m_star_snapshot[pair.x], m_star_snapshot[pair.y]);
        }
        consumed.Dispose();
    }

    void Collide(StarMarker a, StarMarker b)
    {
        if (a == null || b == null) return;
        Vector3 mid = (a.transform.position + b.transform.position) * 0.5f;
        float ra = a.transform.localScale.x * 0.5f;
        float rb = b.transform.localScale.x * 0.5f;
        float peak = (ra + rb) * m_supernova_size_multiplier;

        SpawnSupernova(mid, peak);
        Destroy(a.gameObject);
        Destroy(b.gameObject);
    }

    void SpawnSupernova(Vector3 pos, float peakRadius)
    {
        var go = new GameObject("Supernova");
        go.transform.position = pos;
        var sn = go.AddComponent<Supernova>();
        sn.Init(m_runtime_material, peakRadius, m_supernova_duration);
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    private struct StarCollisionJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float4> stars;
        public NativeQueue<int2>.ParallelWriter collisions;

        public void Execute(int i)
        {
            float4 a = stars[i];
            float3 pa = a.xyz;
            float ra = a.w;
            int len = stars.Length;
            for (int j = i + 1; j < len; j++)
            {
                float4 b = stars[j];
                float3 d = pa - b.xyz;
                float sumR = ra + b.w;
                if (math.lengthsq(d) < sumR * sumR)
                    collisions.Enqueue(new int2(i, j));
            }
        }
    }
}
