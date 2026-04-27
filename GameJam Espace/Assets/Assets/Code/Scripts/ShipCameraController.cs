using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ShipCameraController : MonoBehaviour
{
    public ShipControl m_ship;

    [Header("Soft follow")]
    public float m_follow_responsiveness = 8f;       // 1/s — higher = camera sticks tighter to the ship
    public float m_reference_speed = 200f;           // m/s at which speed-based effects are full
    [Range(0.2f, 1f)] public float m_high_speed_lag = 0.4f;  // 1 = no effect, 0.4 = ~2.5x softer at full speed

    [Header("FOV")]
    public float m_fov_max = 78f;
    public float m_fov_responsiveness = 4f;          // 1/s

    [Header("Boost shake")]
    public float m_boost_shake_amplitude = 0.05f;    // world units, scales with speed_norm

    private Camera m_camera;
    private Vector3 m_local_offset_pos;
    private Quaternion m_local_offset_rot;
    private float m_fov_base;
    private Vector3 m_prev_ship_pos;
    private Quaternion m_prev_ship_rot;
    // Persistent follow position, kept shake-free. transform.position = m_follow_pos
    // + cosmetic shake; if the shake bled into the next frame's feed-forward computation,
    // the random walk would accumulate and the ship would drift in the frustum.
    private Vector3 m_follow_pos;
    // Internal follow rotation, kept free-look-free so the slerp source stays clean.
    // Otherwise free-look would contaminate the next frame's slerp and the rotation
    // would amplify to target_rot * fl^(1/a) instead of target_rot * fl.
    private Quaternion m_follow_rot;

    void Start()
    {
        if (m_ship == null) m_ship = FindAnyObjectByType<ShipControl>();
        m_camera = GetComponent<Camera>();
        m_fov_base = m_camera.fieldOfView;
        if (m_ship == null) return;
        // Capture authored offset of the camera relative to the ship as the rest pose.
        Transform s = m_ship.transform;
        Quaternion inv = Quaternion.Inverse(s.rotation);
        m_local_offset_pos = inv * (transform.position - s.position);
        m_local_offset_rot = inv * transform.rotation;
        m_follow_pos = transform.position;
        m_follow_rot = transform.rotation;
        m_prev_ship_pos = s.position;
        m_prev_ship_rot = s.rotation;
    }

    void LateUpdate()
    {
        if (m_ship == null) return;
        float dt = Time.deltaTime;
        Transform s = m_ship.transform;

        float speed_norm = Mathf.Clamp01(m_ship.VelocityWorld.magnitude / m_reference_speed);
        float lag = Mathf.Lerp(1f, m_high_speed_lag, speed_norm);
        float a = 1f - Mathf.Exp(-m_follow_responsiveness * lag * dt);

        // Step 1 — Feed-forward: apply the ship's rigid frame-to-frame transform to the
        // camera. Without this, a plain Lerp toward a moving target keeps a steady-state
        // error of v(1-a)/a (loses the ship at high speed) and spirals the camera toward
        // the center under pure ship rotation (chord-shortening on a circular target).
        // With it, the offset is invariant to ship motion — only step 2 changes it.
        Quaternion ship_drot = s.rotation * Quaternion.Inverse(m_prev_ship_rot);
        Vector3 cam_offset = m_follow_pos - m_prev_ship_pos;
        m_follow_pos = s.position + ship_drot * cam_offset;
        m_prev_ship_pos = s.position;
        m_prev_ship_rot = s.rotation;

        // Step 2 — Restoring lerp toward the rest pose. Combined with step 1, this is
        // equivalent in ship-local space to: cam_offset_local += a*(m_local_offset_pos
        // - cam_offset_local). Pure first-order LP, zero steady-state error, no drift,
        // no dependency on ship velocity. Same logic for rotation.
        Vector3 target_pos = s.position + s.rotation * m_local_offset_pos;
        Quaternion target_rot = s.rotation * m_local_offset_rot;
        m_follow_pos = Vector3.Lerp(m_follow_pos, target_pos, a);
        m_follow_rot = Quaternion.Slerp(m_follow_rot, target_rot, a);

        Vector2 fl = m_ship.FreeLookEuler;
        transform.rotation = (fl.sqrMagnitude > 0.0001f)
            ? m_follow_rot * Quaternion.Euler(fl.x, fl.y, 0f)
            : m_follow_rot;

        float fov_target = Mathf.Lerp(m_fov_base, m_fov_max, speed_norm);
        m_camera.fieldOfView = Mathf.Lerp(
            m_camera.fieldOfView, fov_target,
            1f - Mathf.Exp(-m_fov_responsiveness * dt));

        // Cosmetic shake: applied on top of m_follow_pos at write-time so it doesn't
        // pollute persistent state (next frame reads m_follow_pos, not transform.position).
        Vector3 shake = (m_ship.IsBoosting && speed_norm > 0.01f)
            ? transform.rotation * (Random.insideUnitSphere * m_boost_shake_amplitude * speed_norm)
            : Vector3.zero;
        transform.position = m_follow_pos + shake;
    }
}
