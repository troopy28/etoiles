using UnityEngine;

// Detects star-star collisions (any pair of StarMarker-tagged objects whose bounding
// spheres overlap) and triggers a supernova at the contact point. Both stars are
// destroyed; their SimGravityBody.OnDestroy unregisters them from the gravity sim.
public class StarCollisionManager : MonoBehaviour
{
    [Header("Collision")]
    public float m_collision_radius_multiplier = 1.0f;   // tweak for more permissive contact

    [Header("Supernova")]
    public float m_supernova_size_multiplier = 6f;       // peak radius = (r_a + r_b) * this
    public float m_supernova_duration = 2.5f;
    public Material m_supernova_material;                // optional override; built from shader if null

    private Material m_runtime_material;

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
            {
                m_runtime_material = new Material(sh);
            }
            else
            {
                Debug.LogWarning("[StarCollisionManager] Shader 'Custom/Supernova' not found. " +
                                 "Add it to Project Settings → Graphics → Always Included Shaders.");
            }
        }
    }

    void FixedUpdate()
    {
        var stars = StarMarker.AllStars;
        int n = stars.Count;
        if (n < 2) return;

        for (int i = 0; i < n; i++)
        {
            var a = stars[i];
            if (a == null) continue;
            float ra = a.transform.localScale.x * 0.5f * m_collision_radius_multiplier;
            Vector3 pa = a.transform.position;

            for (int j = i + 1; j < n; j++)
            {
                var b = stars[j];
                if (b == null) continue;
                float rb = b.transform.localScale.x * 0.5f * m_collision_radius_multiplier;
                float sumR = ra + rb;
                Vector3 d = pa - b.transform.position;
                if (d.sqrMagnitude < sumR * sumR)
                {
                    Collide(a, b);
                    return;   // list will mutate; resume next tick
                }
            }
        }
    }

    void Collide(StarMarker a, StarMarker b)
    {
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
}
