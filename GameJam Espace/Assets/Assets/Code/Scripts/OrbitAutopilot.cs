using UnityEngine;
using Unity.Mathematics;
using Assets.Code.Scripts.Generation;

// Autopilot d'orbite : prend la cible verrouillée par RadarHUD et amène le vaisseau
// sur une orbite circulaire autour de l'étoile. Tant qu'il est en Lock, recharge le
// carburant à un taux modulé par la classe spectrale (O > B > A > F).
//
// State machine: Idle → Approach → Lock → (Idle on disengage / fuel full / manual input).
//
// Le calcul de poussée est exposé via ComputeThrust(dt) ; ShipControl l'appelle depuis
// son FixedUpdate pour que la consommation fuel et les post-fx vitesse continuent de
// fonctionner sans bypass de la chaîne thrust existante.
public class OrbitAutopilot : MonoBehaviour
{
    public enum State { Idle, Approach, Lock }

    [Header("Refs")]
    public ShipControl m_ship;        // auto-found if null
    public RadarHUD m_radar;          // auto-found if null

    [Header("Engage")]
    public float m_engage_max_range = 8000f;   // can't toggle on if target is farther

    [Header("Orbit Geometry")]
    public float m_orbit_radius = 800f;        // target circular orbit radius
    [Range(0.05f, 0.5f)] public float m_lock_radius_tolerance = 0.15f;  // r within ±15% of r_target
    [Range(0.01f, 0.5f)] public float m_lock_radial_speed_tolerance = 0.10f;
    [Range(0.05f, 0.5f)] public float m_lock_tangent_speed_tolerance = 0.15f;

    [Header("Control Gains (Approach)")]
    public float m_radial_position_gain = 0.4f;   // converts r-error → desired v_radial
    public float m_velocity_gain = 1.5f;          // converts velocity-error → acceleration
    public float m_max_radial_speed = 50f;        // clamp on desired |v_radial|
    public float m_thrust_budget_factor = 1.5f;   // autopilot may use up to N× m_thrust_forward

    [Header("Control Gains (Lock)")]
    public float m_lock_radial_correction = 0.1f; // gentle correction to hold r ≈ r_target

    [Header("Refuel")]
    public float m_base_refuel_rate = 8f;         // fuel/sec at class F (multiplier 1×)

    public State CurrentState => m_state;
    public bool IsActive => m_state != State.Idle;
    public bool IsRefueling => m_is_refueling;

    private State m_state = State.Idle;
    private int m_locked_target_id = -1;
    private bool m_is_refueling = false;

    void Start()
    {
        if (m_ship == null) m_ship = GetComponent<ShipControl>();
        if (m_radar == null) m_radar = GetComponent<RadarHUD>();
    }

    public void Toggle()
    {
        if (m_state != State.Idle)
        {
            Disengage();
            return;
        }
        if (m_radar == null || !m_radar.HasTarget) return;
        if (m_radar.CurrentTargetDistance > m_engage_max_range) return;
        m_locked_target_id = m_radar.CurrentTargetId;
        m_state = State.Approach;
        m_is_refueling = false;
    }

    public void Disengage()
    {
        m_state = State.Idle;
        m_locked_target_id = -1;
        m_is_refueling = false;
    }

    // Called by ShipControl when the player gives any thrust input → manual takeover.
    public void NotifyManualInput()
    {
        if (m_state != State.Idle) Disengage();
    }

    // Returns the world-space thrust acceleration the autopilot wants applied this tick.
    // Returns Vector3.zero if not active.
    public Vector3 ComputeThrust(float dt)
    {
        if (m_state == State.Idle) return Vector3.zero;
        if (m_ship == null || m_ship.m_gravity_body == null) { Disengage(); return Vector3.zero; }
        var mgr = m_ship.m_gravity_body.m_manager;
        if (mgr == null || !mgr.m_curr.IsCreated) { Disengage(); return Vector3.zero; }

        // Validate target still exists.
        if (m_locked_target_id < 0 || m_locked_target_id >= mgr.m_curr.Length)
        { Disengage(); return Vector3.zero; }
        float4 target = mgr.m_curr[m_locked_target_id];
        if (target.w == 0f) { Disengage(); return Vector3.zero; }   // unregistered
        // Confirm target is still a refuel-compatible star (e.g., didn't get demoted).
        if (mgr.m_body_kind[m_locked_target_id] != (int)BodyKind.Star ||
            mgr.m_stellar_class[m_locked_target_id] > (int)StellarClass.F)
        { Disengage(); return Vector3.zero; }

        Vector3 P = m_ship.transform.position;
        Vector3 S = (Vector3)target.xyz;
        float M_star = target.w;
        Vector3 V = (Vector3)mgr.GetVelocity(m_ship.m_gravity_body.Id);

        Vector3 r_vec = P - S;
        float r = r_vec.magnitude;
        if (r < 1e-3f) return Vector3.zero;
        Vector3 r_hat = r_vec / r;

        float v_r = Vector3.Dot(V, r_hat);                // radial velocity (+ = away)
        Vector3 v_t_vec = V - v_r * r_hat;                // tangential velocity
        float v_t = v_t_vec.magnitude;

        // Tangent unit vector. If ship's tangential motion is too small to define one,
        // pick a plane using world up vs r_hat.
        Vector3 t_hat;
        if (v_t > 1e-2f) t_hat = v_t_vec / v_t;
        else
        {
            Vector3 polar = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(polar, r_hat)) > 0.95f) polar = Vector3.right;
            t_hat = Vector3.Cross(polar, r_hat).normalized;
        }

        float G = mgr.G;
        float v_orbit = Mathf.Sqrt(Mathf.Max(0f, G * M_star / Mathf.Max(m_orbit_radius, 1e-3f)));

        // Lock condition check (re-evaluated each tick; can fall back to Approach if drifts).
        bool in_radial_band = Mathf.Abs(r - m_orbit_radius) < m_lock_radius_tolerance * m_orbit_radius;
        bool radial_slow = Mathf.Abs(v_r) < m_lock_radial_speed_tolerance * Mathf.Max(v_orbit, 1f);
        bool tangent_match = Mathf.Abs(v_t - v_orbit) < m_lock_tangent_speed_tolerance * Mathf.Max(v_orbit, 1f);

        Vector3 thrust;
        if (m_state == State.Approach)
        {
            if (in_radial_band && radial_slow && tangent_match)
            {
                m_state = State.Lock;
                thrust = ComputeLockThrust(r, r_hat, v_orbit, v_t, t_hat);
            }
            else
            {
                thrust = ComputeApproachThrust(r, r_hat, v_r, v_orbit, v_t, v_t_vec, t_hat);
            }
        }
        else // Lock
        {
            // If we drifted out of the orbital band, re-enter Approach.
            if (!in_radial_band || !radial_slow || !tangent_match)
            {
                m_state = State.Approach;
                thrust = ComputeApproachThrust(r, r_hat, v_r, v_orbit, v_t, v_t_vec, t_hat);
            }
            else
            {
                thrust = ComputeLockThrust(r, r_hat, v_orbit, v_t, t_hat);
            }
        }

        // Refuel only while properly Locked.
        m_is_refueling = false;
        if (m_state == State.Lock && m_ship.m_fuel < m_ship.m_fuel_max)
        {
            float mult = ClassRefuelMultiplier((StellarClass)mgr.m_stellar_class[m_locked_target_id]);
            m_ship.m_fuel = Mathf.Min(m_ship.m_fuel_max, m_ship.m_fuel + m_base_refuel_rate * mult * dt);
            m_is_refueling = true;
            if (m_ship.m_fuel >= m_ship.m_fuel_max - 1e-3f)
            {
                m_ship.m_fuel = m_ship.m_fuel_max;
                Disengage();
            }
        }

        // Clamp total thrust to autopilot budget.
        float budget = m_ship.m_thrust_forward * m_thrust_budget_factor;
        if (thrust.magnitude > budget) thrust = thrust.normalized * budget;
        return thrust;
    }

    Vector3 ComputeApproachThrust(float r, Vector3 r_hat, float v_r,
                                   float v_orbit, float v_t, Vector3 v_t_vec, Vector3 t_hat)
    {
        // Radial: drive r → r_target with v_radial → 0.
        float r_error = r - m_orbit_radius;
        float desired_v_r = Mathf.Clamp(-m_radial_position_gain * r_error,
                                         -m_max_radial_speed, m_max_radial_speed);
        float a_r = m_velocity_gain * (desired_v_r - v_r);

        // Tangential: drive v_t → v_orbit along t_hat.
        // Note: t_hat already aligned with v_t_vec when v_t was large enough.
        float a_t = m_velocity_gain * (v_orbit - v_t);

        return a_r * r_hat + a_t * t_hat;
    }

    Vector3 ComputeLockThrust(float r, Vector3 r_hat, float v_orbit, float v_t, Vector3 t_hat)
    {
        // Lock = let gravity carry the orbit. Apply a tiny radial correction only.
        float r_error = r - m_orbit_radius;
        float a_r = -m_lock_radial_correction * r_error;
        // Light tangential trim if velocity drifts.
        float a_t = m_lock_radial_correction * (v_orbit - v_t);
        return a_r * r_hat + a_t * t_hat;
    }

    public static float ClassRefuelMultiplier(StellarClass c)
    {
        switch (c)
        {
            case StellarClass.O: return 4.0f;
            case StellarClass.B: return 2.5f;
            case StellarClass.A: return 1.5f;
            case StellarClass.F: return 1.0f;
        }
        return 0f;
    }

    void OnGUI()
    {
        if (m_ship == null || !m_ship.m_show_hud) return;
        if (m_state == State.Idle) return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperRight,
            fontSize = Mathf.RoundToInt(Screen.height * 0.022f),
            fontStyle = FontStyle.Bold
        };
        float pad = Screen.height * 0.015f;
        float line_h = style.fontSize * 1.3f;
        // Slot below TRAJECTORY (3rd line in ShipControl HUD).
        float y = pad + 3f * line_h;

        Color prev = GUI.color;
        if (m_state == State.Approach)
        {
            GUI.color = new Color(1f, 0.85f, 0.4f, 1f);
            GUI.Label(new Rect(0, y, Screen.width - pad, line_h),
                "AUTO-ORBIT (O) : APPROACHING", style);
        }
        else // Lock
        {
            // Pulse green during refuel.
            float pulse = m_is_refueling ? (Mathf.Sin(Time.unscaledTime * Mathf.PI * 4f) + 1f) * 0.5f : 1f;
            GUI.color = m_is_refueling
                ? Color.Lerp(new Color(0.4f, 1f, 0.5f, 0.5f), new Color(0.5f, 1f, 0.6f, 1f), pulse)
                : new Color(0.4f, 1f, 0.6f, 1f);
            GUI.Label(new Rect(0, y, Screen.width - pad, line_h),
                m_is_refueling ? "AUTO-ORBIT (O) : REFUELING" : "AUTO-ORBIT (O) : LOCKED", style);
        }
        GUI.color = prev;
    }
}
