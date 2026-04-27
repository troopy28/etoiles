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

    void ApplyMaterialState(float t)
    {
        if (m_material == null) return;

        Color core = Color.Lerp(new Color(1f, 1f, 1f, 1f), new Color(1f, 0.5f, 0.1f, 1f), t);
        Color edge = Color.Lerp(new Color(1f, 0.7f, 0.2f, 1f), new Color(0.6f, 0.08f, 0.04f, 1f), t);
        float brightness = Mathf.Lerp(5f, 1.2f, t);
        float alpha = Mathf.Pow(1f - t, 1.5f);

        if (m_material.HasProperty("_Color"))      m_material.SetColor("_Color", core);
        if (m_material.HasProperty("_EdgeColor"))  m_material.SetColor("_EdgeColor", edge);
        if (m_material.HasProperty("_Brightness")) m_material.SetFloat("_Brightness", brightness);
        if (m_material.HasProperty("_Alpha"))      m_material.SetFloat("_Alpha", alpha);
    }

    void OnDestroy()
    {
        if (m_material != null) Destroy(m_material);
    }
}
