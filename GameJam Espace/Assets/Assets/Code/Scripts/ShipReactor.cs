using UnityEngine;

public enum ReactorAxis
{
    MainForward,
    RetroBack,
    StrafeLeft,
    StrafeRight,
    VerticalUp,
    VerticalDown
}

// One component per reactor. Drop the GameObject anywhere under the ship root,
// with a child cylinder primitive (the flame mesh) wearing ReactorFlame.mat.
// Orient the cylinder so its LOCAL +Y axis points in the direction the exhaust
// gases flow (i.e. opposite to the thrust direction this reactor represents).
// Reads thrust state from ShipControl in parent and drives the material via
// MaterialPropertyBlock + transform scale each frame.
[RequireComponent(typeof(Transform))]
public class ShipReactor : MonoBehaviour
{
    public ReactorAxis m_axis = ReactorAxis.MainForward;

    [Header("Visuals")]
    public Renderer m_renderer;             // cylinder MeshRenderer; auto-found in children if null
    public Light m_light;                    // optional point light pulsing with throttle

    [Header("Response (frame-rate independent attack/decay)")]
    public float m_attack_time = 0.08f;
    public float m_decay_time  = 0.35f;

    [Header("Edge color (Fresnel rim) per regime")]
    public Color m_color_normal = new Color(0.30f, 0.60f, 1.00f, 1f);   // deep blue
    public Color m_color_boost  = new Color(0.70f, 0.95f, 1.00f, 1f);   // hotter cyan-white
    public Color m_color_brake  = new Color(1.00f, 0.50f, 0.10f, 1f);   // RCS retro orange

    [Header("Core color (always hot white-yellow)")]
    public Color m_core_color = new Color(1.0f, 0.95f, 0.75f, 1f);

    [Header("Driven ranges (lerped by smoothed throttle)")]
    // Length / width are multipliers applied to the cylinder's initial localScale.
    // 1.0 means the size you set in the editor; 0 means collapsed.
    public Vector2 m_length_range     = new Vector2(0f,   1f);
    public Vector2 m_width_range      = new Vector2(0.6f, 1f);
    public Vector2 m_brightness_range = new Vector2(2f,   8f);
    public Vector2 m_alpha_range      = new Vector2(0f,   1f);
    public Vector2 m_light_intensity  = new Vector2(0f,   3f);

    private static readonly int s_idCoreColor  = Shader.PropertyToID("_CoreColor");
    private static readonly int s_idEdgeColor  = Shader.PropertyToID("_EdgeColor");
    private static readonly int s_idBrightness = Shader.PropertyToID("_Brightness");
    private static readonly int s_idAlpha      = Shader.PropertyToID("_Alpha");
    private static readonly int s_idAnimTime   = Shader.PropertyToID("_AnimTime");

    private ShipControl m_ship;
    private MaterialPropertyBlock m_mpb;
    private float m_smoothed = 0f;
    private Vector3 m_initial_scale = Vector3.one;   // captured at Awake; ranges are multipliers of this
    // Position of the cylinder's BASE (mesh's lowest point along local Y) in the parent's
    // local frame. We keep the base pinned here while only the length grows toward the tip.
    private Vector3 m_base_local_pos = Vector3.zero;
    // Half-height of the mesh in object space along Y (1.0 for the Unity cylinder primitive,
    // 0.5 for a unit cube, etc.). Captured from the mesh bounds so any axis-Y mesh works.
    private float m_mesh_half_height = 1f;

    void Awake()
    {
        m_ship = GetComponentInParent<ShipControl>();
        if (m_renderer == null) m_renderer = GetComponentInChildren<Renderer>();
        m_mpb = new MaterialPropertyBlock();
        if (m_renderer != null)
        {
            Transform t = m_renderer.transform;
            m_initial_scale = t.localScale;
            m_mesh_half_height = m_renderer.localBounds.extents.y;
            // Cylinder's local -Y points toward its base. Express in parent frame via the
            // cylinder's localRotation, offset by the mesh's half-height times initial scale.
            Vector3 base_dir_parent = t.localRotation * Vector3.down;
            m_base_local_pos = t.localPosition + base_dir_parent * (m_mesh_half_height * m_initial_scale.y);
        }
    }

    void LateUpdate()
    {
        if (m_ship == null || m_renderer == null) return;

        // Effective direction in the ship's logical frame.
        // - Braking: ShipControl overrides thrust with a counter-velocity accel.
        //   We read that accel (world space), bring it back to logical frame, and
        //   normalize it to a unit direction so per-axis components are 0..1.
        // - Otherwise: the raw player input is already in logical frame.
        Vector3 logical_dir;
        if (m_ship.IsBraking)
        {
            Vector3 accel_world = m_ship.ThrustAccelWorld;
            Vector3 accel_logical = m_ship.LogicalToLocal *
                (Quaternion.Inverse(m_ship.transform.rotation) * accel_world);
            float mag = accel_logical.magnitude;
            logical_dir = (mag > 0.01f) ? accel_logical / mag : Vector3.zero;
        }
        else
        {
            logical_dir = m_ship.ThrustInputLogical;
        }

        float target = AxisComponent(logical_dir, m_axis);

        // Asymmetric attack/decay smoothing, frame-rate independent.
        float tau = (target > m_smoothed) ? m_attack_time : m_decay_time;
        float k = (tau > 1e-4f) ? (1f - Mathf.Exp(-Time.deltaTime / tau)) : 1f;
        m_smoothed = Mathf.Lerp(m_smoothed, target, k);

        Color edge_tint = m_ship.IsBraking  ? m_color_brake
                        : m_ship.IsBoosting ? m_color_boost
                                            : m_color_normal;

        // Drive transform scale as multipliers of the cylinder's initial scale,
        // so the size set in the editor is preserved at full throttle.
        float length_mul = Mathf.Lerp(m_length_range.x, m_length_range.y, m_smoothed);
        float width_mul  = Mathf.Lerp(m_width_range.x,  m_width_range.y,  m_smoothed);
        Transform t = m_renderer.transform;
        float new_length = m_initial_scale.y * length_mul;
        t.localScale = new Vector3(
            m_initial_scale.x * width_mul,
            new_length,
            m_initial_scale.z * width_mul);
        // Keep the base (mesh's lowest Y point) pinned at its captured anchor; only the tip moves.
        Vector3 tip_dir_parent = t.localRotation * Vector3.up;
        t.localPosition = m_base_local_pos + tip_dir_parent * (m_mesh_half_height * new_length);

        // Drive shader via MPB so all reactors share a single material instance.
        m_renderer.GetPropertyBlock(m_mpb);
        m_mpb.SetColor(s_idCoreColor,  m_core_color);
        m_mpb.SetColor(s_idEdgeColor,  edge_tint);
        m_mpb.SetFloat(s_idBrightness, Mathf.Lerp(m_brightness_range.x, m_brightness_range.y, m_smoothed));
        m_mpb.SetFloat(s_idAlpha,      Mathf.Lerp(m_alpha_range.x,      m_alpha_range.y,      m_smoothed));
        m_mpb.SetFloat(s_idAnimTime,   Time.time);
        m_renderer.SetPropertyBlock(m_mpb);

        if (m_light != null)
        {
            m_light.color = edge_tint;
            m_light.intensity = Mathf.Lerp(m_light_intensity.x, m_light_intensity.y, m_smoothed);
        }
    }

    static float AxisComponent(Vector3 dir, ReactorAxis axis)
    {
        switch (axis)
        {
            case ReactorAxis.MainForward:  return Mathf.Max(0f,  dir.z);
            case ReactorAxis.RetroBack:    return Mathf.Max(0f, -dir.z);
            case ReactorAxis.StrafeRight:  return Mathf.Max(0f,  dir.x);
            case ReactorAxis.StrafeLeft:   return Mathf.Max(0f, -dir.x);
            case ReactorAxis.VerticalUp:   return Mathf.Max(0f,  dir.y);
            case ReactorAxis.VerticalDown: return Mathf.Max(0f, -dir.y);
            default: return 0f;
        }
    }
}
