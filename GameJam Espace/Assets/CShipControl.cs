using UnityEngine;
using UnityEngine.InputSystem;

public class CShipControl : MonoBehaviour
{
    [Header("Thrust")]
    public float m_thrust_forward = 20f;
    public float m_thrust_lateral = 10f;
    public float m_thrust_vertical = 10f;
    public float m_boost_multiplier = 3f;

    [Header("Rotation")]
    public float m_pitch_speed = 60f;   // degrees/second at full input
    public float m_yaw_speed = 60f;
    public float m_yaw_accel = 5f;      // how fast yaw ramps up/down (higher = snappier)
    public float m_roll_speed = 90f;
    [Range(0.1f, 2f)] public float m_mouse_sensitivity = 1f;
    // All ratios are expressed as fraction of screen height (resolution-independent):
    [Range(0.1f, 1f)] public float m_mouse_max_offset_ratio = 0.5f;   // virtual cursor max distance (50% of screen height)
    [Range(0f, 0.1f)] public float m_mouse_deadzone_ratio = 0.02f;    // dead zone radius (2% of screen height)
    [Range(1f, 4f)] public float m_mouse_response_curve = 3f;          // 1 = linear, higher = more precision near center

    [Header("Flight Assist")]
    public float m_linear_damping = 0.5f;   // velocity bleed-off rate

    [Header("Free Look")]
    public Transform m_camera;                          // child camera transform (auto-found if null)
    public float m_free_look_sensitivity = 0.2f;
    public float m_free_look_pitch_limit = 80f;
    public float m_free_look_return_speed = 12f;         // how fast camera snaps back when toggled off

    [Header("HUD")]
    public bool m_show_hud = true;
    [Range(0.05f, 0.3f)] public float m_hud_radius_ratio = 0.1f;  // indicator area radius (as fraction of screen height)
    public Color m_hud_color = new Color(0.4f, 1f, 0.6f, 0.9f);

    private Vector3 m_velocity = Vector3.zero;
    private Vector2 m_virtual_mouse = Vector2.zero;   // accumulated mouse offset from center
    private int m_mouse_skip_frames = 2;              // skip initial frames to avoid cursor-lock jump
    private float m_yaw_current = 0f;                 // smoothed yaw input (for inertia on A/E)
    private Vector2 m_effective_input = Vector2.zero; // smoothed pitch/roll input (for HUD display)
    private bool m_free_look = false;
    private Vector2 m_free_look_euler = Vector2.zero; // pitch (x), yaw (y)
    private static Texture2D s_white_tex;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        // Auto-find camera if not set (first Camera in children)
        if (m_camera == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) m_camera = cam.transform;
        }
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (kb.escapeKey.wasPressedThisFrame)
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked
                ? CursorLockMode.None : CursorLockMode.Locked;

        float dt = Time.deltaTime;

        // --- Rotation: Elite Dangerous "virtual joystick" style ---
        // Mouse moves a virtual cursor around a center point; offset from center
        // determines rotation RATE (not instant rotation). Dead zone in the middle.
        // Mouse Y → pitch, Mouse X → roll, A/E → yaw
        // Note: Input System uses PHYSICAL QWERTY positions, not printed characters.
        // Convert screen-ratio params to absolute pixels (resolution-independent)
        float max_offset_px = m_mouse_max_offset_ratio * Screen.height;
        float deadzone_px = m_mouse_deadzone_ratio * Screen.height;

        Vector2 mouseDelta = mouse.delta.ReadValue();
        // Skip first frames to ignore the large delta caused by cursor-lock snap
        if (m_mouse_skip_frames > 0)
        {
            m_mouse_skip_frames--;
            mouseDelta = Vector2.zero;
        }

        // Toggle free look on middle mouse click
        if (mouse.middleButton.wasPressedThisFrame)
        {
            m_free_look = !m_free_look;
            if (!m_free_look)
                m_free_look_euler = Vector2.zero;   // reset target; camera will lerp back
        }

        if (m_free_look)
        {
            // Free look: mouse rotates camera independently of ship. Ship rotation frozen.
            m_free_look_euler.x = Mathf.Clamp(
                m_free_look_euler.x - mouseDelta.y * m_free_look_sensitivity,
                -m_free_look_pitch_limit, m_free_look_pitch_limit);
            m_free_look_euler.y += mouseDelta.x * m_free_look_sensitivity;
            if (m_camera != null)
                m_camera.localRotation = Quaternion.Euler(m_free_look_euler.x, m_free_look_euler.y, 0f);

            m_effective_input = Vector2.zero;    // no HUD arrow while looking around
        }
        else
        {
            // Smoothly snap camera back to ship-aligned when exiting free look
            if (m_camera != null)
                m_camera.localRotation = Quaternion.Slerp(
                    m_camera.localRotation,
                    Quaternion.identity,
                    1f - Mathf.Exp(-m_free_look_return_speed * dt));

            m_virtual_mouse += mouseDelta * m_mouse_sensitivity;

            // Clamp virtual cursor to max offset radius
            if (m_virtual_mouse.magnitude > max_offset_px)
                m_virtual_mouse = m_virtual_mouse.normalized * max_offset_px;

            // Apply dead zone: below threshold → no rotation
            Vector2 effective = Vector2.zero;
            float offset_mag = m_virtual_mouse.magnitude;
            if (offset_mag > deadzone_px)
            {
                // Remap (deadzone .. max_offset) → (0 .. 1), then apply response curve
                // for finer control near center (t^2 = quadratic, t^3 = cubic, etc.)
                float t = (offset_mag - deadzone_px) / (max_offset_px - deadzone_px);
                t = Mathf.Pow(t, m_mouse_response_curve);
                effective = m_virtual_mouse.normalized * t;
            }

            m_effective_input = effective;   // cached for HUD

            float pitch = -effective.y * m_pitch_speed * dt;
            float roll = -effective.x * m_roll_speed * dt;

            float yaw_target = 0f;
            if (kb.qKey.isPressed) yaw_target -= 1f;   // AZERTY A → yaw left
            if (kb.eKey.isPressed) yaw_target += 1f;   // AZERTY E → yaw right
            // Smoothly ramp current yaw toward target for inertia effect
            m_yaw_current = Mathf.Lerp(m_yaw_current, yaw_target, 1f - Mathf.Exp(-m_yaw_accel * dt));
            float yaw = m_yaw_current * m_yaw_speed * dt;

            transform.Rotate(pitch, yaw, roll, Space.Self);
        }

        // --- Thrust: ZQSD (AZERTY) forward/strafe, Space/Ctrl vertical ---
        Vector3 thrust_input = Vector3.zero;
        if (kb.wKey.isPressed) thrust_input.z += 1f;  // AZERTY Z → forward
        if (kb.sKey.isPressed) thrust_input.z -= 1f;  // AZERTY S → backward
        if (kb.aKey.isPressed) thrust_input.x -= 1f;  // AZERTY Q → strafe left
        if (kb.dKey.isPressed) thrust_input.x += 1f;  // AZERTY D → strafe right
        if (kb.rKey.isPressed) thrust_input.y += 1f;  // thrust up
        if (kb.fKey.isPressed) thrust_input.y -= 1f;  // thrust down

        float boost = kb.leftShiftKey.isPressed ? m_boost_multiplier : 1f;

        // X: cut throttle (emergency stop)
        if (kb.xKey.wasPressedThisFrame)
            m_velocity = Vector3.zero;

        // Apply thrust in ship's local space
        Vector3 thrust_world = transform.TransformDirection(new Vector3(
            thrust_input.x * m_thrust_lateral,
            thrust_input.y * m_thrust_vertical,
            thrust_input.z * m_thrust_forward
        )) * boost;

        m_velocity += thrust_world * dt;

        // Flight assist: exponential velocity damping
        m_velocity *= Mathf.Exp(-m_linear_damping * dt);

        transform.position += m_velocity * dt;
    }

    void OnGUI()
    {
        if (!m_show_hud) return;

        // Lazy-init 1x1 white texture used for all draw calls
        if (s_white_tex == null)
        {
            s_white_tex = new Texture2D(1, 1);
            s_white_tex.SetPixel(0, 0, Color.white);
            s_white_tex.Apply();
        }

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        float radius = Screen.height * m_hud_radius_ratio;

        // Map effective input (already in -1..1 range) to screen radius
        // OnGUI Y is inverted compared to input space
        float tip_x = cx + m_effective_input.x * radius;
        float tip_y = cy - m_effective_input.y * radius;

        Color prev = GUI.color;

        // Faint center reticle (ring of 4 ticks)
        GUI.color = new Color(m_hud_color.r, m_hud_color.g, m_hud_color.b, 0.3f);
        GUI.DrawTexture(new Rect(cx - radius - 1, cy - 1, 3, 2), s_white_tex);
        GUI.DrawTexture(new Rect(cx + radius - 1, cy - 1, 3, 2), s_white_tex);
        GUI.DrawTexture(new Rect(cx - 1, cy - radius - 1, 2, 3), s_white_tex);
        GUI.DrawTexture(new Rect(cx - 1, cy + radius - 1, 2, 3), s_white_tex);

        // Stylized arrowhead (chevron) pointing in rotation direction
        if (m_effective_input.sqrMagnitude > 0.0001f)
        {
            Vector2 to = new Vector2(tip_x, tip_y);
            Vector2 dir = m_effective_input.normalized;
            // Flip Y because OnGUI Y is inverted vs input space
            dir.y = -dir.y;

            float head_size = 20f;
            float head_angle = 35f * Mathf.Deg2Rad;
            float cos = Mathf.Cos(head_angle);
            float sin = Mathf.Sin(head_angle);
            Vector2 back = -dir;
            Vector2 left_wing = new Vector2(
                back.x * cos - back.y * sin,
                back.x * sin + back.y * cos) * head_size;
            Vector2 right_wing = new Vector2(
                back.x * cos + back.y * sin,
                -back.x * sin + back.y * cos) * head_size;

            // Alpha scales linearly with distance from center (invisible at center, opaque at max).
            // Undo the response curve so opacity reflects actual cursor offset, not rotation speed.
            float alpha_scale = Mathf.Clamp01(
                Mathf.Pow(m_effective_input.magnitude, 1f / m_mouse_response_curve));
            Color arrow_color = m_hud_color;
            arrow_color.a *= alpha_scale;
            DrawLine(to, to + left_wing, arrow_color, 3f);
            DrawLine(to, to + right_wing, arrow_color, 3f);
        }

        // Center dot
        GUI.color = m_hud_color;
        GUI.DrawTexture(new Rect(cx - 2, cy - 2, 4, 4), s_white_tex);

        GUI.color = prev;
    }

    static void DrawLine(Vector2 from, Vector2 to, Color color, float thickness)
    {
        Matrix4x4 prev = GUI.matrix;
        Vector2 delta = to - from;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        GUIUtility.RotateAroundPivot(angle, from);
        Color c = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(from.x, from.y - thickness * 0.5f, delta.magnitude, thickness), s_white_tex);
        GUI.color = c;
        GUI.matrix = prev;
    }
}
