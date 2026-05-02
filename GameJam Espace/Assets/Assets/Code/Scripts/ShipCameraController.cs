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
    public float m_boost_shake_amplitude = 0.025f;   // world units, scales with speed_norm
    public float m_shake_ease_speed = 5f;            // 1/s — how fast shake ramps in/out

    private float m_shake_intensity = 0f;            // smoothed 0..1, eased in/out
    private Camera m_camera;
    private Vector3 m_local_offset_pos;
    private Quaternion m_local_offset_rot;
    private float m_fov_base;
    // Pose caméra suivie en repère ship-local (petit vecteur). La conversion en monde
    // se fait à la dernière étape, ce qui évite la cancellation fp32 sur les coordonnées
    // monde grandes (la sim grav. travaille en double, mais le ship est rendu loin de
    // l'origine et un follow tracké en monde y dérive en bruit ~ε·|world_pos|).
    private Vector3 m_local_follow_pos;
    private Quaternion m_local_follow_rot;

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
        m_local_follow_pos = m_local_offset_pos;
        m_local_follow_rot = m_local_offset_rot;
    }

    void LateUpdate()
    {
        if (m_ship == null) return;
        float dt = Time.deltaTime;
        Transform s = m_ship.transform;

        float speed_norm = Mathf.Clamp01(m_ship.VelocityWorld.magnitude / m_reference_speed);
        float lag = Mathf.Lerp(1f, m_high_speed_lag, speed_norm);
        float a = 1f - Mathf.Exp(-m_follow_responsiveness * lag * dt);

        // Suivi en repère ship-local. Sous le mouvement rigide du vaisseau, l'offset
        // local est invariant (pas besoin de feed-forward) ; le lerp converge vers la
        // pose de repos. Travailler en petits vecteurs évite la cancellation fp32 que
        // donnait l'ancien suivi monde quand le vaisseau s'éloignait de l'origine.
        m_local_follow_pos = Vector3.Lerp(m_local_follow_pos, m_local_offset_pos, a);
        m_local_follow_rot = Quaternion.Slerp(m_local_follow_rot, m_local_offset_rot, a);

        Quaternion follow_rot_world = s.rotation * m_local_follow_rot;
        Vector2 fl = m_ship.FreeLookEuler;
        transform.rotation = (fl.sqrMagnitude > 0.0001f)
            ? follow_rot_world * Quaternion.Euler(fl.x, fl.y, 0f)
            : follow_rot_world;

        float fov_target = Mathf.Lerp(m_fov_base, m_fov_max, speed_norm);
        m_camera.fieldOfView = Mathf.Lerp(
            m_camera.fieldOfView, fov_target,
            1f - Mathf.Exp(-m_fov_responsiveness * dt));

        // Cosmetic shake: only when boosting AND thrusting forward (Z key).
        // Eased in/out so the shake fades smoothly when conditions change.
        bool shake_active = m_ship.IsBoosting && m_ship.ThrustInputLogical.z > 0f;
        float shake_target = shake_active ? 1f : 0f;
        m_shake_intensity = Mathf.Lerp(
            m_shake_intensity, shake_target,
            1f - Mathf.Exp(-m_shake_ease_speed * dt));

        // Shake calculé en local et additionné au petit offset avant la conversion en
        // monde : une seule addition grand+petit, pas de bruit fp32 dépendant de la
        // distance à l'origine.
        Vector3 shake_local = (m_shake_intensity > 0.001f)
            ? Random.insideUnitSphere * m_boost_shake_amplitude * speed_norm * m_shake_intensity
            : Vector3.zero;

        transform.position = s.position + s.rotation * (m_local_follow_pos + shake_local);
    }
}
