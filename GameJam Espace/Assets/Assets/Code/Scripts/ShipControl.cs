using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public class ShipControl : MonoBehaviour
{
    [Header("Thrust")]
    public float m_thrust_forward = 20f;
    public float m_thrust_lateral = 10f;
    public float m_thrust_vertical = 10f;
    public float m_thrust_brake = 15f;   // max brake acceleration magnitude (B key)
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

    [Header("Free Look")]
    public float m_free_look_sensitivity = 0.2f;
    public float m_free_look_pitch_limit = 80f;
    public float m_free_look_return_speed = 12f;         // how fast camera snaps back when toggled off

    [Header("HUD")]
    public bool m_show_hud = true;
    [Range(0.05f, 0.3f)] public float m_hud_radius_ratio = 0.1f;  // indicator area radius (as fraction of screen height)
    public Color m_hud_color = new Color(0.4f, 1f, 0.6f, 0.9f);

    [Header("Physics Body")]
    public SimGravityBody m_gravity_body;   // auto-found if null; must have mass > 0

    [Header("Fuel")]
    public float m_fuel_max = 100f;
    public float m_fuel = 100f;
    public float m_fuel_consumption_factor = 0.05f;  // fuel/sec per unit of |m_thrust_accel|
    public bool m_infinite_fuel = false;             // when true: no consumption, locked at 100%

    [Header("Autopilot")]
    public OrbitAutopilot m_autopilot;   // auto-found if null; controls thrust during orbit autopilot

    [Header("Trajectory Prediction (ENTER to trigger)")]
    public float m_trajectory_dt = 0.5f;          // simulation step for prediction (s)
    public int m_trajectory_samples = 100;        // number of points / frames to build
    public Color m_trajectory_color = Color.green;
    public float m_trajectory_width = 0.5f;
    public float m_trajectory_fade_speed = 1.5f;  // alpha decay per second after build complete

    [Header("Targeting (left click)")]
    public bool m_targeting_enabled = true;
    public float m_target_halo_thickness = 2.5f;   // pixels (constant screen size, distance-compensated)
    public float m_target_halo_padding = 1.4f;     // halo radius = body radius * this
    public float m_target_halo_min_pixels = 26f;   // minimum halo radius in pixels (for tiny far bodies)
    public float m_target_pickup_min_pixels = 14f; // minimum cursor capture radius in pixels
    [Range(0f, 0.5f)] public float m_target_panel_bottom_reserve = 0.30f;  // fraction of screen height reserved at bottom (clears the radar)

    [Header("Postfx vitesse")]
    public Volume m_postfx_volume;                // global volume containing LensDistortion + MotionBlur overrides
    public AnimationCurve m_lens_distortion_by_speed =
        new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(100f, -0.5f));
    public AnimationCurve m_motion_blur_by_speed =
        new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(100f, 0.5f));

    private LensDistortion m_lens_distortion;
    private MotionBlur m_motion_blur;

    private enum TrajState { Idle, Building, Fading }
    private TrajState m_traj_state = TrajState.Idle;
    private float m_traj_alpha = 0f;
    private Vector3 m_traj_anchor;             // ship world pos when prediction was started
    private LineRenderer m_trajectory_line;
    // Persistent prediction buffers (one verlet step per frame).
    private NativeArray<float4> m_pred_curr;
    private NativeArray<float4> m_pred_prev;
    private NativeArray<float3> m_pred_external_acc;   // snapshot of live thrust at start of cycle
    private bool m_pred_allocated = false;
    private int m_pred_index = 0;
    private Vector3[] m_pred_buffer;           // stored as DELTAS from m_traj_anchor

    private Vector3 m_thrust_accel = Vector3.zero;    // current thrust acceleration (world space), submitted to sim each FixedUpdate
    private bool m_cut_throttle_pending = false;
    private bool m_braking = false;                   // true while B is held (for HUD readout)
    private bool m_boosting = false;                  // true while Shift is held (for HUD readout)
    private Vector2 m_virtual_mouse = Vector2.zero;   // accumulated mouse offset from center
    private int m_mouse_skip_frames = 2;              // skip initial frames to avoid cursor-lock jump
    private float m_yaw_current = 0f;                 // smoothed yaw input (for inertia on A/E)
    private Vector2 m_effective_input = Vector2.zero; // smoothed pitch/roll input (for HUD display)
    private bool m_free_look = false;
    private Vector2 m_free_look_euler = Vector2.zero; // pitch (x), yaw (y)

    // Targeting state
    private SimGravityBody m_hovered_body;             // body currently under reticle
    private SimGravityBody m_selected_body;            // body selected by left-click; persists until next click
    private Camera m_target_cam;                       // cached reference; refreshed each tick if null
    private LineRenderer m_halo_hover;                 // 3D worldspace halo (post-fx affects it like the rest of the scene)
    private LineRenderer m_halo_selected;              // separate ring for the locked target (thicker)
    private const int HALO_SEGMENTS = 64;
    private Vector3[] m_halo_points;

    private static Texture2D s_white_tex;

    // Rotation initiale du transform — capturée au Start. Permet au script de raisonner
    // dans un repère "logique" (Y up, Z forward) indépendant de l'orientation du prefab/FBX.
    private Quaternion m_initial_rotation = Quaternion.identity;
    private Quaternion m_logical_to_local = Quaternion.identity;  // = Inverse(m_initial_rotation)

    // Public read-only state for VFX systems (ShipReactor, future audio system).
    public Vector3 ThrustInputLogical { get; private set; }   // raw -1..1 per logical axis (pre-boost, pre-brake)
    public Vector3 ThrustAccelWorld   => m_thrust_accel;       // current world-space accel actually pushed to sim
    public Quaternion LogicalToLocal  => m_logical_to_local;
    public bool IsBoosting => m_boosting;
    public bool IsBraking  => m_braking;
    public bool IsRefueling => m_autopilot != null && m_autopilot.IsRefueling;
    // Read-only state consumed by ShipCameraController (soft-follow camera).
    public Vector3 VelocityWorld =>
        (m_gravity_body != null && m_gravity_body.m_manager != null)
            ? (Vector3)m_gravity_body.m_manager.GetVelocity(m_gravity_body.Id)
            : Vector3.zero;
    public Vector2 FreeLookEuler => m_free_look_euler;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        m_initial_rotation = transform.rotation;
        m_logical_to_local = Quaternion.Inverse(m_initial_rotation);
        if (m_gravity_body == null)
            m_gravity_body = GetComponent<SimGravityBody>();
        if (m_autopilot == null)
            m_autopilot = GetComponent<OrbitAutopilot>();
        SetupTrajectoryLine();
        SetupTargetHalos();

        // Grab overrides once. volume.profile (not sharedProfile) returns a per-instance
        // copy so modifying intensity at runtime won't dirty the asset on disk.
        if (m_postfx_volume != null && m_postfx_volume.profile != null)
        {
            m_postfx_volume.profile.TryGet(out m_lens_distortion);
            m_postfx_volume.profile.TryGet(out m_motion_blur);
        }
    }

    void SetupTargetHalos()
    {
        m_halo_points = new Vector3[HALO_SEGMENTS];
        m_halo_hover = CreateHaloLineRenderer("TargetHalo_Hover");
        m_halo_selected = CreateHaloLineRenderer("TargetHalo_Selected");
    }

    LineRenderer CreateHaloLineRenderer(string name)
    {
        var go = new GameObject(name);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = HALO_SEGMENTS;
        lr.numCornerVertices = 0;
        lr.numCapVertices = 0;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        lr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        lr.enabled = false;
        return lr;
    }

    void SetupTrajectoryLine()
    {
        GameObject go = new GameObject("TrajectoryLine");
        go.transform.SetParent(transform, false);
        m_trajectory_line = go.AddComponent<LineRenderer>();
        m_trajectory_line.useWorldSpace = true;
        m_trajectory_line.startWidth = m_trajectory_width;
        m_trajectory_line.endWidth = m_trajectory_width;
        m_trajectory_line.startColor = m_trajectory_color;
        m_trajectory_line.endColor = m_trajectory_color;
        // Sprites/Default supports vertex color and works in URP & built-in.
        m_trajectory_line.material = new Material(Shader.Find("Sprites/Default"));
        m_trajectory_line.positionCount = 0;
        m_trajectory_line.enabled = false;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (kb.escapeKey.wasPressedThisFrame)
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked
                ? CursorLockMode.None : CursorLockMode.Locked;

        // T triggers a one-shot trajectory prediction (re-press to restart)
        if (kb.tKey.wasPressedThisFrame)
            StartTrajectoryPrediction();

        // O toggles orbit autopilot (engages on the radar's current target)
        if (kb.oKey.wasPressedThisFrame && m_autopilot != null)
            m_autopilot.Toggle();

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

        // Toggle free look on middle mouse click. m_free_look_euler is consumed by
        // ShipCameraController each LateUpdate; when disabled, it converges to zero
        // smoothly below, which makes the camera return to its rest orientation.
        if (mouse.middleButton.wasPressedThisFrame)
            m_free_look = !m_free_look;

        if (m_free_look)
        {
            // Free look: mouse rotates camera independently of ship. Ship rotation frozen.
            m_free_look_euler.x = Mathf.Clamp(
                m_free_look_euler.x - mouseDelta.y * m_free_look_sensitivity,
                -m_free_look_pitch_limit, m_free_look_pitch_limit);
            m_free_look_euler.y += mouseDelta.x * m_free_look_sensitivity;

            m_effective_input = Vector2.zero;    // no HUD arrow while looking around
        }
        else
        {
            // Free-look off: smoothly converge euler to zero so the camera controller
            // returns the view to the rest orientation.
            m_free_look_euler = Vector2.Lerp(
                m_free_look_euler, Vector2.zero,
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

            // Apply rotation around the LOGICAL axes (not the local axes, which may be
            // skewed by the prefab's initial rotation). Conjugating Δ by m_initial_rotation
            // turns a "logical-frame delta" into the equivalent local-frame delta.
            Quaternion delta_logical = Quaternion.Euler(pitch, yaw, roll);
            transform.rotation = transform.rotation * m_logical_to_local * delta_logical * m_initial_rotation;
        }

        // --- Thrust: ZQSD (AZERTY) forward/strafe, Space/Ctrl vertical ---
        Vector3 thrust_input = Vector3.zero;
        if (kb.wKey.isPressed) thrust_input.z += 1f;  // AZERTY Z → forward
        if (kb.sKey.isPressed) thrust_input.z -= 1f;  // AZERTY S → backward
        if (kb.aKey.isPressed) thrust_input.x -= 1f;  // AZERTY Q → strafe left
        if (kb.dKey.isPressed) thrust_input.x += 1f;  // AZERTY D → strafe right
        if (kb.rKey.isPressed) thrust_input.y += 1f;  // thrust up
        if (kb.fKey.isPressed) thrust_input.y -= 1f;  // thrust down
        ThrustInputLogical = thrust_input;

        // Manual input or brake while autopilot active → disengage (player takes over).
        if (m_autopilot != null && m_autopilot.IsActive &&
            (thrust_input != Vector3.zero || kb.bKey.isPressed))
            m_autopilot.NotifyManualInput();

        m_boosting = kb.leftShiftKey.isPressed;
        float boost = m_boosting ? m_boost_multiplier : 1f;

        // X: cut throttle (emergency stop) — deferred to FixedUpdate so it hits the sim state
        if (kb.xKey.wasPressedThisFrame)
            m_cut_throttle_pending = true;

        // Thrust as acceleration in world space. Position integration is handled
        // by SimGravityManager (gravity + this external acceleration, via Verlet).
        // Input is in the LOGICAL frame; (transform.rotation * m_logical_to_local)
        // is the logical frame's current world orientation.
        Vector3 logical_thrust = new Vector3(
            thrust_input.x * m_thrust_lateral,
            thrust_input.y * m_thrust_vertical,
            thrust_input.z * m_thrust_forward
        );
        m_thrust_accel = (transform.rotation * m_logical_to_local * logical_thrust) * boost;

        // B: brake RCS — override thrust with counter-velocity acceleration, capped at m_thrust_brake.
        // Required accel to null velocity in one fixed tick is -vel / fixedDt; clamped so we never overshoot.
        // Boost (SHIFT) also amplifies the brake cap, mirroring its effect on thrust.
        m_braking = kb.bKey.isPressed;
        if (m_braking && m_gravity_body != null && m_gravity_body.m_manager != null)
        {
            Vector3 vel = (Vector3)m_gravity_body.m_manager.GetVelocity(m_gravity_body.Id);
            Vector3 required = -vel / Time.fixedDeltaTime;
            float mag = required.magnitude;
            float brake_cap = m_thrust_brake * boost;
            if (mag > brake_cap) required *= brake_cap / mag;
            m_thrust_accel = required;
        }

        // No fuel → no thrust output. Consumption itself is metered in FixedUpdate.
        if (m_fuel <= 0f)
        {
            m_fuel = 0f;
            m_thrust_accel = Vector3.zero;
            m_braking = false;
        }

        UpdateSpeedPostfx();
        UpdateTargeting(mouse);
    }

    void UpdateTargeting(Mouse mouse)
    {
        if (!m_targeting_enabled) { m_hovered_body = null; return; }
        if (m_target_cam == null) m_target_cam = Camera.main;
        if (m_target_cam == null) return;

        // Reticle = exact screen center.
        Vector2 reticle = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector3 cam_right = m_target_cam.transform.right;

        SimGravityBody best = null;
        float best_score = -1f;
        var all = SimGravityBody.AllRegistered;
        for (int i = 0; i < all.Count; i++)
        {
            var body = all[i];
            if (body == null || body == m_gravity_body) continue;

            Vector3 sp = m_target_cam.WorldToScreenPoint(body.transform.position);
            if (sp.z <= 0f) continue;   // behind camera
            Vector2 sp2 = new Vector2(sp.x, sp.y);

            // Body's screen-space radius via projecting an offset along camera right.
            Vector3 edge_w = body.transform.position + cam_right * (body.transform.localScale.x * 0.5f);
            Vector3 edge_s = m_target_cam.WorldToScreenPoint(edge_w);
            float screen_r = Vector2.Distance(new Vector2(edge_s.x, edge_s.y), sp2);
            float pickup = Mathf.Max(screen_r, m_target_pickup_min_pixels);

            if (Vector2.Distance(sp2, reticle) > pickup) continue;

            // Prefer larger screen radius (stars dominate when far, planets dominate when close).
            // Small kind weight so stars edge out planets in close calls (e.g. transit).
            float kind_weight = (body.kind == BodyKind.Star) ? 1.5f : 1f;
            float score = screen_r * kind_weight;
            if (score > best_score)
            {
                best_score = score;
                best = body;
            }
        }
        m_hovered_body = best;

        // Left click: lock-in selection (or clear if clicked into empty space).
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            m_selected_body = m_hovered_body;
    }

    void UpdateSpeedPostfx()
    {
        float speed = VelocityWorld.magnitude;
        if (m_lens_distortion != null)
            m_lens_distortion.intensity.value = m_lens_distortion_by_speed.Evaluate(speed);
        if (m_motion_blur != null)
            m_motion_blur.intensity.value = Mathf.Clamp01(m_motion_blur_by_speed.Evaluate(speed));
    }

    void FixedUpdate()
    {
        if (m_gravity_body == null || m_gravity_body.m_manager == null) return;
        int id = m_gravity_body.Id;

        // Autopilot overrides the manually-computed thrust. Routed through m_thrust_accel
        // so fuel consumption, post-fx vitesse et reactor VFX restent cohérents.
        // ComputeThrust() also performs the refuel tick when in Lock — appelé même à fuel=0
        // pour ne pas bloquer le rechargement quand le réservoir est vide en orbite.
        if (m_autopilot != null && m_autopilot.IsActive)
        {
            m_thrust_accel = m_autopilot.ComputeThrust(Time.fixedDeltaTime);
            if (m_fuel <= 0f) m_thrust_accel = Vector3.zero;
        }

        // Consume fuel proportionally to applied thrust magnitude (covers thrust + brake RCS).
        if (m_infinite_fuel)
        {
            m_fuel = m_fuel_max;
        }
        else if (m_fuel > 0f)
        {
            float burn = m_thrust_accel.magnitude * m_fuel_consumption_factor * Time.fixedDeltaTime;
            m_fuel = Mathf.Max(0f, m_fuel - burn);
        }

        m_gravity_body.m_manager.SetExternalAcceleration(id, m_thrust_accel);
        if (m_cut_throttle_pending)
        {
            m_gravity_body.m_manager.ZeroVelocity(id);
            m_cut_throttle_pending = false;
        }
    }

    void LateUpdate()
    {
        UpdateTargetHalos();
        UpdateTrajectoryState();
    }

    void UpdateTrajectoryState()
    {
        if (m_trajectory_line == null) return;
        if (m_traj_state == TrajState.Idle)
        {
            if (m_trajectory_line.enabled) m_trajectory_line.enabled = false;
            return;
        }
        if (m_gravity_body == null || m_gravity_body.m_manager == null) return;

        if (m_traj_state == TrajState.Building)
        {
            StepPrediction();
            UpdateTrajectoryDisplay();
            if (m_pred_index >= m_trajectory_samples)
                m_traj_state = TrajState.Fading;
        }
        else if (m_traj_state == TrajState.Fading)
        {
            m_traj_alpha -= m_trajectory_fade_speed * Time.deltaTime;
            if (m_traj_alpha <= 0f)
            {
                m_traj_alpha = 0f;
                m_traj_state = TrajState.Idle;
                m_trajectory_line.enabled = false;
                m_trajectory_line.positionCount = 0;
                return;
            }
            UpdateTrajectoryDisplay();
        }
    }

    void UpdateTargetHalos()
    {
        if (m_halo_hover == null) return;

        // Hovered halo (thin)
        UpdateOneHalo(m_halo_hover, m_hovered_body, m_target_halo_thickness);

        // Selected halo (thicker), only when distinct from hovered
        bool show_selected = m_selected_body != null && m_selected_body != m_hovered_body;
        UpdateOneHalo(m_halo_selected, show_selected ? m_selected_body : null, m_target_halo_thickness * 1.6f);
    }

    void UpdateOneHalo(LineRenderer lr, SimGravityBody body, float pixelThickness)
    {
        if (lr == null) return;
        if (body == null || m_target_cam == null)
        {
            if (lr.enabled) lr.enabled = false;
            return;
        }

        Vector3 center = body.transform.position;
        Vector3 toCam = m_target_cam.transform.position - center;
        // Skip if camera is inside the body or behind us — avoids degenerate cases.
        float distance = toCam.magnitude;
        if (distance < 0.01f) { lr.enabled = false; return; }

        // World units per screen pixel at this distance, for constant pixel sizing.
        float fovRad = m_target_cam.fieldOfView * Mathf.Deg2Rad;
        float worldPerPixel = (2f * distance * Mathf.Tan(fovRad * 0.5f)) / Mathf.Max(1f, Screen.height);

        float bodyRadiusWorld = body.transform.localScale.x * 0.5f;
        float worldRadius = Mathf.Max(bodyRadiusWorld * m_target_halo_padding,
                                      m_target_halo_min_pixels * worldPerPixel);
        float worldThickness = pixelThickness * worldPerPixel;

        Vector3 right = m_target_cam.transform.right;
        Vector3 up = m_target_cam.transform.up;
        for (int i = 0; i < HALO_SEGMENTS; i++)
        {
            float a = (i / (float)HALO_SEGMENTS) * Mathf.PI * 2f;
            m_halo_points[i] = center + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * worldRadius;
        }
        lr.SetPositions(m_halo_points);
        lr.startColor = m_hud_color;
        lr.endColor = m_hud_color;
        lr.startWidth = worldThickness;
        lr.endWidth = worldThickness;
        if (!lr.enabled) lr.enabled = true;
    }

    void StartTrajectoryPrediction()
    {
        if (m_gravity_body == null || m_gravity_body.m_manager == null) return;

        var mgr = m_gravity_body.m_manager;
        int n = mgr.m_curr.Length;
        if (n == 0) return;

        // (Re)allocate native buffers if body count changed.
        if (!m_pred_allocated || m_pred_curr.Length != n)
        {
            DisposePredictionBuffers();
            m_pred_curr = new NativeArray<float4>(n, Allocator.Persistent);
            m_pred_prev = new NativeArray<float4>(n, Allocator.Persistent);
            m_pred_external_acc = new NativeArray<float3>(n, Allocator.Persistent);
            m_pred_allocated = true;
        }
        if (m_pred_buffer == null || m_pred_buffer.Length != m_trajectory_samples)
            m_pred_buffer = new Vector3[m_trajectory_samples];

        NativeArray<float4>.Copy(mgr.m_curr.AsArray(), m_pred_curr);
        NativeArray<float4>.Copy(mgr.m_prev.AsArray(), m_pred_prev);
        NativeArray<float3>.Copy(mgr.m_external_acc.AsArray(), m_pred_external_acc);

        // Rescale prev so (curr - prev) corresponds to velocity * trajectory_dt.
        float scale = m_trajectory_dt / Time.fixedDeltaTime;
        if (Mathf.Abs(scale - 1f) > 1e-6f)
        {
            for (int i = 0; i < n; i++)
            {
                float3 disp = m_pred_curr[i].xyz - m_pred_prev[i].xyz;
                m_pred_prev[i] = new float4(m_pred_curr[i].xyz - disp * scale, m_pred_prev[i].w);
            }
        }

        m_pred_index = 0;
        m_traj_anchor = transform.position;
        m_traj_alpha = 1f;
        m_traj_state = TrajState.Building;
        m_trajectory_line.positionCount = 0;
        m_trajectory_line.enabled = true;
    }

    void StepPrediction()
    {
        if (!m_pred_allocated || m_pred_index >= m_trajectory_samples) return;
        var mgr = m_gravity_body.m_manager;
        float dt2 = m_trajectory_dt * m_trajectory_dt;
        var job = new SimGravityManager.NBodyVerletJob
        {
            curr = m_pred_curr,
            prev = m_pred_prev,
            external_acc = m_pred_external_acc,
            G_dt2 = mgr.G * dt2,
            dt2 = dt2
        };
        job.Schedule(m_pred_curr.Length, 64).Complete();
        var tmp = m_pred_curr; m_pred_curr = m_pred_prev; m_pred_prev = tmp;
        // Store as DELTA from the anchor (ship pos at prediction start) so the
        // line can be drawn relative to the ship's current position.
        m_pred_buffer[m_pred_index] = (Vector3)m_pred_curr[m_gravity_body.Id].xyz - m_traj_anchor;
        m_pred_index++;
    }

    void UpdateTrajectoryDisplay()
    {
        Vector3 ship_pos = transform.position;
        // First vertex is anchored to the ship's current position; remaining vertices
        // are predicted samples (deltas), so the line visibly starts at the ship.
        int count = m_pred_index + 1;
        if (m_trajectory_line.positionCount != count)
            m_trajectory_line.positionCount = count;
        m_trajectory_line.SetPosition(0, ship_pos);
        for (int i = 0; i < m_pred_index; i++)
            m_trajectory_line.SetPosition(i + 1, ship_pos + m_pred_buffer[i]);

        m_trajectory_line.startWidth = m_trajectory_width;
        m_trajectory_line.endWidth = m_trajectory_width;
        Color c = m_trajectory_color;
        c.a *= m_traj_alpha;
        m_trajectory_line.startColor = c;
        m_trajectory_line.endColor = c;
    }

    void DisposePredictionBuffers()
    {
        if (!m_pred_allocated) return;
        m_pred_curr.Dispose();
        m_pred_prev.Dispose();
        m_pred_external_acc.Dispose();
        m_pred_allocated = false;
    }

    void OnDestroy()
    {
        DisposePredictionBuffers();
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

        // Top-right status readouts (boost, then brake)
        Color prev_color = GUI.color;
        GUIStyle status_style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperRight,
            fontSize = Mathf.RoundToInt(Screen.height * 0.022f),
            fontStyle = FontStyle.Bold
        };
        float pad = Screen.height * 0.015f;
        float line_h = status_style.fontSize * 1.3f;

        GUI.color = m_boosting ? new Color(0.4f, 0.8f, 1f, 1f) : m_hud_color;
        GUI.Label(
            new Rect(0, pad, Screen.width - pad, line_h),
            "BOOST (SHIFT) : " + (m_boosting ? "ON" : "OFF"),
            status_style);

        GUI.color = m_braking ? new Color(1f, 0.5f, 0.3f, 1f) : m_hud_color;
        GUI.Label(
            new Rect(0, pad + line_h, Screen.width - pad, line_h),
            "BRAKING (B) : " + (m_braking ? "ON" : "OFF"),
            status_style);

        bool traj_active = m_traj_state != TrajState.Idle;
        GUI.color = traj_active ? m_trajectory_color : m_hud_color;
        GUI.Label(
            new Rect(0, pad + 2f * line_h, Screen.width - pad, line_h),
            "TRAJECTORY (T) : " + (traj_active ? "ON" : "OFF"),
            status_style);

        // Bottom-right vertical fuel gauge
        float gauge_w = Screen.height * 0.03f;
        float gauge_h = Screen.height * 0.30f;
        float gauge_pad_x = Screen.height * 0.03f;
        float gauge_pad_y = Screen.height * 0.025f;
        float text_h = status_style.fontSize * 1.4f;
        float gauge_x = Screen.width - gauge_pad_x - gauge_w;
        float gauge_bot_y = Screen.height - gauge_pad_y - text_h;
        float gauge_top_y = gauge_bot_y - gauge_h;

        float fuel_ratio = m_fuel_max > 0f ? Mathf.Clamp01(m_fuel / m_fuel_max) : 0f;
        Color fuel_color = fuel_ratio < 0.2f
            ? Color.Lerp(new Color(1f, 0.2f, 0.15f, 1f), new Color(1f, 0.6f, 0.2f, 1f), fuel_ratio / 0.2f)
            : m_hud_color;
        if (IsRefueling)
        {
            float refuel_pulse = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 4f) + 1f) * 0.5f;
            fuel_color = Color.Lerp(new Color(0.4f, 1f, 0.5f, 0.65f),
                                    new Color(0.6f, 1f, 0.65f, 1f), refuel_pulse);
        }

        // Empty (background)
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(new Rect(gauge_x, gauge_top_y, gauge_w, gauge_h), s_white_tex);
        // Fill (from bottom up)
        float fill_h = gauge_h * fuel_ratio;
        GUI.color = fuel_color;
        GUI.DrawTexture(new Rect(gauge_x, gauge_bot_y - fill_h, gauge_w, fill_h), s_white_tex);
        // Border (1px, 4 sides)
        GUI.color = new Color(m_hud_color.r, m_hud_color.g, m_hud_color.b, 0.7f);
        GUI.DrawTexture(new Rect(gauge_x, gauge_top_y, gauge_w, 1), s_white_tex);
        GUI.DrawTexture(new Rect(gauge_x, gauge_bot_y, gauge_w, 1), s_white_tex);
        GUI.DrawTexture(new Rect(gauge_x, gauge_top_y, 1, gauge_h), s_white_tex);
        GUI.DrawTexture(new Rect(gauge_x + gauge_w - 1, gauge_top_y, 1, gauge_h), s_white_tex);

        // Top label: percentage with 0.1 precision
        GUIStyle gauge_label_style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(Screen.height * 0.018f),
            fontStyle = FontStyle.Bold
        };
        float label_w = gauge_w * 4f;
        float label_x = gauge_x + gauge_w * 0.5f - label_w * 0.5f;
        GUI.color = fuel_color;
        GUI.Label(
            new Rect(label_x, gauge_top_y - text_h, label_w, text_h),
            (fuel_ratio * 100f).ToString("F1") + "%",
            gauge_label_style);
        // Bottom label: FUEL — blinks red below 10%
        Color fuel_label_color = m_hud_color;
        if (fuel_ratio < 0.1f)
        {
            float blink = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 4f) + 1f) * 0.5f;   // 0..1 at 2Hz
            fuel_label_color = Color.Lerp(new Color(1f, 0.1f, 0.1f, 0.35f),
                                          new Color(1f, 0.15f, 0.15f, 1f), blink);
        }
        GUI.color = fuel_label_color;
        GUI.Label(
            new Rect(label_x, gauge_bot_y, label_w, text_h),
            "FUEL",
            gauge_label_style);

        GUI.color = prev_color;

        // Targeting info panel (halos are 3D LineRenderers, drawn in scene by Unity).
        if (m_selected_body != null)
            DrawTargetInfoPanel(m_selected_body);

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

    void DrawTargetInfoPanel(SimGravityBody body)
    {
        if (body == null) return;

        Vector3 ship_pos = transform.position;
        Vector3 body_pos = body.transform.position;
        float distance = Vector3.Distance(ship_pos, body_pos);

        Vector3 ship_vel = (m_gravity_body != null && m_gravity_body.m_manager != null)
            ? (Vector3)m_gravity_body.m_manager.GetVelocity(m_gravity_body.Id)
            : Vector3.zero;
        Vector3 body_vel = (body.m_manager != null)
            ? (Vector3)body.m_manager.GetVelocity(body.Id)
            : Vector3.zero;
        float rel_speed = (ship_vel - body_vel).magnitude;

        string spectral = body.kind == BodyKind.Star ? body.spectral_class.ToString() : "—";

        // Two-column rows: labels left-aligned (with colon), values aligned in a fixed column.
        (string label, string value)[] rows =
        {
            ("Type:",       body.kind.ToString()),
            ("Mass:",       body.mass.ToString("G3")),
            ("Spectral:",   spectral),
            ("Refuelable:", "NON"),
            ("Distance:",   distance.ToString("F1")),
            ("Rel. speed:", rel_speed.ToString("F1")),
        };

        float pad = Screen.height * 0.02f;
        float fontSize = Mathf.Max(11f, Screen.height * 0.016f);
        float lineH = fontSize * 1.4f;
        float headerH = lineH * 1.3f;
        float innerPad = 10f;
        float panelH = headerH + lineH * rows.Length + innerPad * 2f;
        float panelW = Mathf.Max(280f, Screen.width * 0.20f);

        float bottom_reserve = Screen.height * m_target_panel_bottom_reserve;
        Rect panel = new Rect(pad, Screen.height - pad - panelH - bottom_reserve, panelW, panelH);

        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(panel, s_white_tex);

        GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = Mathf.RoundToInt(fontSize * 1.05f),
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(0, 0, 0, 0),
        };
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = Mathf.RoundToInt(fontSize),
            fontStyle = FontStyle.Normal,
            padding = new RectOffset(0, 0, 0, 0),
        };
        GUIStyle valueStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = Mathf.RoundToInt(fontSize),
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(0, 0, 0, 0),
        };

        GUI.color = m_hud_color;

        float x = panel.x + innerPad;
        float y = panel.y + innerPad;
        float contentW = panel.width - innerPad * 2f;

        // Header (full-width)
        GUI.Label(new Rect(x, y, contentW, headerH), $"TARGET: {body.name}", headerStyle);
        y += headerH;

        // Two-column rows
        float labelColW = contentW * 0.42f;
        float gap = 8f;
        float valueX = x + labelColW + gap;
        float valueColW = contentW - labelColW - gap;
        for (int i = 0; i < rows.Length; i++)
        {
            GUI.Label(new Rect(x, y, labelColW, lineH), rows[i].label, labelStyle);
            GUI.Label(new Rect(valueX, y, valueColW, lineH), rows[i].value, valueStyle);
            y += lineH;
        }

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
