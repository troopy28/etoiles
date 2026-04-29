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
// orient it so the flame shoots toward -Z local, attach a child ParticleSystem
// (Stretched Billboard, Local sim space, additive). Reads thrust state from
// ShipControl in parent and drives the PS + optional Light each frame.
[RequireComponent(typeof(Transform))]
public class ShipReactor : MonoBehaviour
{
    public ReactorAxis m_axis = ReactorAxis.MainForward;

    [Header("Visuals")]
    public ParticleSystem m_flame;
    public Light m_light;

    [Header("Response (frame-rate independent attack/decay)")]
    public float m_attack_time = 0.08f;
    public float m_decay_time  = 0.35f;

    [Header("Color (multiplied with the PS's authored Color over Lifetime)")]
    public Color m_color_normal = new Color(0.30f, 0.60f, 1.00f, 1f);   // deep blue
    public Color m_color_boost  = new Color(0.70f, 0.95f, 1.00f, 1f);   // hotter cyan-white
    public Color m_color_brake  = new Color(1.00f, 0.50f, 0.10f, 1f);   // RCS retro orange

    [Header("Driven ranges (lerped by smoothed throttle)")]
    public Vector2 m_size_range      = new Vector2(0.2f, 1.0f);   // PS startSize multiplier
    public Vector2 m_emission_range  = new Vector2(0f, 250f);     // particles/sec
    public Vector2 m_light_intensity = new Vector2(0f, 3f);

    private ShipControl m_ship;
    private float m_smoothed = 0f;

    void Awake()
    {
        m_ship = GetComponentInParent<ShipControl>();
    }

    void LateUpdate()
    {
        if (m_ship == null || m_flame == null) return;

        // Effective direction in the ship's logical frame.
        // - Braking: ShipControl overrides thrust with a counter-velocity accel.
        //   We read that accel (world space), bring it back to logical frame, and
        //   normalize it to a unit direction so per-axis components are 0..1.
        // - Otherwise: the raw player input is already in logical frame.
        Vector3 logical_dir;
        if (m_ship.IsBraking)
        {
            Vector3 accel_world = m_ship.ThrustAccelWorld;
            Vector3 accel_logical = m_ship.LogicalToLocal * (Quaternion.Inverse(m_ship.transform.rotation) * accel_world);
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

        Color tint = m_ship.IsBraking ? m_color_brake
                   : m_ship.IsBoosting ? m_color_boost
                                       : m_color_normal;

        var main = m_flame.main;
        main.startSizeMultiplier = Mathf.Lerp(m_size_range.x, m_size_range.y, m_smoothed);
        main.startColor = tint;

        var emission = m_flame.emission;
        emission.rateOverTimeMultiplier = Mathf.Lerp(m_emission_range.x, m_emission_range.y, m_smoothed);

        if (m_light != null)
        {
            m_light.color = tint;
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
