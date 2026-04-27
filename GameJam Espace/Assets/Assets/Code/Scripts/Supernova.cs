using UnityEngine;

// Self-managed supernova VFX: an additive sphere that expands (ease-out), shifts color
// from white-hot to red, and fades out. Self-destroys after the configured duration.
public class Supernova : MonoBehaviour
{
    private float m_age = 0f;
    private float m_duration = 2.5f;
    private float m_peak_radius = 10f;
    private MeshRenderer m_renderer;
    private Material m_material;

    private static Mesh s_sphere_mesh;

    public void Init(Material baseMaterial, float peakRadius, float duration)
    {
        m_peak_radius = Mathf.Max(0.01f, peakRadius);
        m_duration = Mathf.Max(0.05f, duration);

        if (s_sphere_mesh == null)
        {
            // Borrow Unity's built-in sphere mesh; remove the unwanted collider.
            var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s_sphere_mesh = probe.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(probe);
        }

        var mf = gameObject.AddComponent<MeshFilter>();
        mf.sharedMesh = s_sphere_mesh;
        m_renderer = gameObject.AddComponent<MeshRenderer>();
        m_renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        m_renderer.receiveShadows = false;
        m_renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        m_renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        m_material = baseMaterial != null
            ? new Material(baseMaterial)
            : new Material(Shader.Find("Custom/Supernova"));
        m_renderer.sharedMaterial = m_material;

        transform.localScale = Vector3.one * 0.1f;
        ApplyMaterialState(0f);
    }

    void Update()
    {
        m_age += Time.deltaTime;
        float t = m_age / m_duration;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        // Ease-out expansion (fast at start, slowing down).
        float scale_t = 1f - Mathf.Pow(1f - t, 3f);
        float diameter = Mathf.Lerp(0.1f, m_peak_radius * 2f, scale_t);
        transform.localScale = Vector3.one * diameter;

        ApplyMaterialState(t);
    }

    // Black-body cooling curve: blue-white → white → cream → yellow → orange → dark red.
    // Late-life: core fades faster than edge → "hollow shell" appearance (bright rim, dim center).
    private static readonly (float t, Color c)[] s_core_keys =
    {
        (0.00f, new Color(1.5f, 1.7f, 2.2f)),     // blue-white intense (HDR for bloom)
        (0.10f, new Color(1.0f, 1.0f, 1.0f)),     // pure white
        (0.30f, new Color(1.0f, 0.95f, 0.7f)),    // warm white / cream
        (0.55f, new Color(1.0f, 0.85f, 0.3f)),    // yellow
        (0.75f, new Color(0.6f, 0.25f, 0.06f)),   // dimming orange
        (1.00f, new Color(0.10f, 0.015f, 0.01f)), // near-extinct (hollow center)
    };
    private static readonly (float t, Color c)[] s_edge_keys =
    {
        (0.00f, new Color(1.0f, 1.0f, 0.9f)),     // pale yellow-white
        (0.20f, new Color(1.0f, 0.7f, 0.2f)),     // amber
        (0.55f, new Color(1.2f, 0.4f, 0.08f)),    // hot orange (HDR rim)
        (0.80f, new Color(1.0f, 0.18f, 0.04f)),   // bright red
        (1.00f, new Color(0.7f, 0.10f, 0.05f)),   // visible red rim (still hot vs core)
    };
    private static readonly (float t, float v)[] s_brightness_keys =
    {
        (0.00f, 12f),
        (0.10f, 8f),
        (0.30f, 4f),
        (0.70f, 1.5f),
        (1.00f, 0.7f),
    };
    // Vertex displacement amount (object space units; sphere radius is 0.5).
    // Starts perfectly round at the flash, grows to a heavily fragmented shell.
    private static readonly (float t, float v)[] s_displace_keys =
    {
        (0.00f, 0.00f),
        (0.15f, 0.10f),
        (0.40f, 0.35f),
        (0.70f, 0.60f),
        (1.00f, 0.75f),
    };

    static Color EvalGradient((float t, Color c)[] keys, float t)
    {
        if (t <= keys[0].t) return keys[0].c;
        for (int i = 0; i < keys.Length - 1; i++)
        {
            if (t < keys[i + 1].t)
            {
                float u = (t - keys[i].t) / (keys[i + 1].t - keys[i].t);
                return Color.Lerp(keys[i].c, keys[i + 1].c, u);
            }
        }
        return keys[keys.Length - 1].c;
    }

    static float EvalCurve((float t, float v)[] keys, float t)
    {
        if (t <= keys[0].t) return keys[0].v;
        for (int i = 0; i < keys.Length - 1; i++)
        {
            if (t < keys[i + 1].t)
            {
                float u = (t - keys[i].t) / (keys[i + 1].t - keys[i].t);
                return Mathf.Lerp(keys[i].v, keys[i + 1].v, u);
            }
        }
        return keys[keys.Length - 1].v;
    }

    void ApplyMaterialState(float t)
    {
        if (m_material == null) return;

        Color core = EvalGradient(s_core_keys, t);
        Color edge = EvalGradient(s_edge_keys, t);
        float brightness = EvalCurve(s_brightness_keys, t);
        float displace = EvalCurve(s_displace_keys, t);
        float alpha = Mathf.Pow(1f - t, 1.2f);   // softer fade than previous 1.5

        if (m_material.HasProperty("_Color"))      m_material.SetColor("_Color", core);
        if (m_material.HasProperty("_EdgeColor"))  m_material.SetColor("_EdgeColor", edge);
        if (m_material.HasProperty("_Brightness")) m_material.SetFloat("_Brightness", brightness);
        if (m_material.HasProperty("_Alpha"))      m_material.SetFloat("_Alpha", alpha);
        if (m_material.HasProperty("_Displace"))   m_material.SetFloat("_Displace", displace);
        if (m_material.HasProperty("_AnimTime"))   m_material.SetFloat("_AnimTime", m_age);
    }

    void OnDestroy()
    {
        if (m_material != null) Destroy(m_material);
    }
}
