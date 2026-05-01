using UnityEngine;
using Unity.Mathematics;
using Assets.Code.Scripts.Generation;

// Mini-radar 2D circulaire (bottom-left HUD). Display only: the actual target is owned
// by ShipControl (m_selected_body), driven by left-click / 1 / 2. The radar simply
// projects whatever body the ship has selected onto a forward-cone polar plot.
public class RadarHUD : MonoBehaviour
{
    [Header("Refs")]
    public ShipControl m_ship;                // auto-found if null
    public Camera m_camera;                   // auto-found via Camera.main if null — defines the radar's "forward" axis

    [Header("Visuals")]
    [Range(0.05f, 0.2f)] public float m_radius_ratio = 0.09f;   // radar radius as fraction of screen height
    public Color m_frame_color = new Color(1f, 0.6f, 0.15f, 0.9f);    // amber/orange HUD theme
    public Color m_no_target_color = new Color(0.6f, 0.6f, 0.6f, 0.7f);

    public bool HasTarget => m_ship != null && m_ship.SelectedBody != null;
    public int CurrentTargetId => HasTarget ? m_ship.SelectedBody.Id : -1;
    public Vector3 CurrentTargetPos => HasTarget ? m_ship.SelectedBody.transform.position : Vector3.zero;
    public float CurrentTargetDistance =>
        HasTarget ? Vector3.Distance(m_ship.transform.position, m_ship.SelectedBody.transform.position) : 0f;
    public StellarClass CurrentTargetClass =>
        (HasTarget && m_ship.SelectedBody.kind == BodyKind.Star) ? m_ship.SelectedBody.spectral_class : StellarClass.G;
    public bool TargetIsStar => HasTarget && m_ship.SelectedBody.kind == BodyKind.Star;
    public float CurrentTargetRelativeSpeed
    {
        get
        {
            if (!HasTarget) return 0f;
            var ship_body = m_ship.m_gravity_body;
            var target = m_ship.SelectedBody;
            if (ship_body == null || ship_body.m_manager == null) return 0f;
            if (target.m_manager == null) return 0f;
            Vector3 ship_v = (Vector3)ship_body.m_manager.GetVelocity(ship_body.Id);
            Vector3 target_v = (Vector3)target.m_manager.GetVelocity(target.Id);
            return (ship_v - target_v).magnitude;
        }
    }

    private static Texture2D s_white_tex;

    void Start()
    {
        if (m_ship == null) m_ship = GetComponent<ShipControl>();
        if (m_camera == null) m_camera = Camera.main;
    }

    void OnGUI()
    {
        if (m_ship == null || !m_ship.m_show_hud) return;

        if (s_white_tex == null)
        {
            s_white_tex = new Texture2D(1, 1);
            s_white_tex.SetPixel(0, 0, Color.white);
            s_white_tex.Apply();
        }

        float radius = Screen.height * m_radius_ratio;
        float pad = Screen.height * 0.025f;
        float text_size = Screen.height * 0.018f;
        float cx = pad + radius;
        float cy = Screen.height - pad - radius - text_size * 2.5f;

        Color prev = GUI.color;

        // Background disc (semi-transparent black)
        DrawDisc(cx, cy, radius, new Color(0f, 0f, 0f, 0.45f));
        // Frame ring
        DrawRing(cx, cy, radius, m_frame_color, 1.5f);
        // Inner concentric ring (visual reference, 50% range)
        DrawRing(cx, cy, radius * 0.5f, new Color(m_frame_color.r, m_frame_color.g, m_frame_color.b, 0.35f), 1f);
        // Cross axes
        GUI.color = new Color(m_frame_color.r, m_frame_color.g, m_frame_color.b, 0.25f);
        GUI.DrawTexture(new Rect(cx - radius, cy - 0.5f, radius * 2f, 1f), s_white_tex);
        GUI.DrawTexture(new Rect(cx - 0.5f, cy - radius, 1f, radius * 2f), s_white_tex);
        // Center dot = ship
        GUI.color = m_frame_color;
        GUI.DrawTexture(new Rect(cx - 2f, cy - 2f, 4f, 4f), s_white_tex);

        GUIStyle label_style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize = Mathf.RoundToInt(text_size),
            fontStyle = FontStyle.Bold
        };

        // Mode banner above the radar — tells the player whether the target is auto-tracked
        // (refuel / arrival) or pinned by left-click. Always rendered, even when no target.
        DrawModeBanner(cx, cy, radius, text_size, label_style);

        if (!HasTarget)
        {
            GUI.color = m_no_target_color;
            GUI.Label(
                new Rect(cx - radius, cy + radius + 2f, radius * 2f, text_size * 1.4f),
                "NO TARGET",
                label_style);
            GUI.color = prev;
            return;
        }

        Vector3 target_pos = CurrentTargetPos;
        float target_distance = CurrentTargetDistance;

        // Forward-cone polar projection: angular distance from the camera's forward axis
        // maps to the blip's distance from the radar center. Center = target dead ahead;
        // inner ring (0.5×radius) = perpendicular (90°); rim = dead behind.
        // Using the camera frame (rather than the ship transform) means free-look and the
        // soft-follow lag are honoured, so the radar always matches what the player sees.
        // Fallback to the ship's logical frame (Z forward, Y up) if no camera is bound;
        // never use raw transform.InverseTransformDirection since the FBX has a -90/-90/0
        // rest rotation that would skew the projection.
        Vector3 world_offset = target_pos - m_ship.transform.position;
        Vector3 local;
        if (m_camera != null)
        {
            local = m_camera.transform.InverseTransformDirection(world_offset);
        }
        else
        {
            Quaternion w2l = Quaternion.Inverse(m_ship.transform.rotation * m_ship.LogicalToLocal);
            local = w2l * world_offset;
        }
        Vector3 dir = local.sqrMagnitude > 1e-6f ? local.normalized : Vector3.forward;

        float forward = Mathf.Clamp(dir.z, -1f, 1f);
        float theta = Mathf.Acos(forward);          // 0 (ahead) .. π (behind)
        float r_norm = theta / Mathf.PI;            // 0..1
        // Azimuth in the (right, up) plane. When dir is collinear with forward, this is
        // undefined but r_norm is also 0 (or 1 for behind), so the choice doesn't matter
        // visually — pick a stable default.
        Vector2 azimuth = new Vector2(dir.x, dir.y);
        if (azimuth.sqrMagnitude > 1e-6f) azimuth.Normalize();
        else azimuth = Vector2.up;

        // GUI Y is inverted vs world up.
        float blip_x = cx + azimuth.x * r_norm * radius;
        float blip_y = cy - azimuth.y * r_norm * radius;

        bool behind = forward < 0f;
        Color blip_color = TargetIsStar ? StarClassColor(CurrentTargetClass) : m_frame_color;
        // Dim the blip slightly when in the rear hemisphere — visual cue that the player
        // needs to turn around. Color stays the same so the class is still readable.
        if (behind) blip_color.a *= 0.7f;

        GUI.color = blip_color;
        GUI.DrawTexture(new Rect(blip_x - 4f, blip_y - 4f, 8f, 8f), s_white_tex);
        // Outline pulse — bigger and faster when behind to draw attention.
        float pulse = (Mathf.Sin(Time.unscaledTime * (behind ? 6f : 4f)) + 1f) * 0.5f;
        float ring_r = (behind ? 8f : 6f) + pulse * 5f;
        Color ring_c = blip_color;
        ring_c.a *= (1f - pulse) * 0.8f;
        DrawRing(blip_x, blip_y, ring_r, ring_c, 1.5f);

        // Big "TURN AROUND" warning overlaid on the radar when the target is in the rear
        // hemisphere. Pulses red to be impossible to miss.
        if (behind)
        {
            float warn_pulse = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 3f) + 1f) * 0.5f;
            Color warn_color = Color.Lerp(new Color(1f, 0.3f, 0.2f, 0.6f),
                                          new Color(1f, 0.5f, 0.4f, 1f), warn_pulse);
            GUIStyle warn_style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(text_size * 1.15f),
                fontStyle = FontStyle.Bold
            };
            GUI.color = warn_color;
            GUI.Label(
                new Rect(cx - radius, cy - text_size * 0.7f, radius * 2f, text_size * 1.4f),
                "TURN AROUND",
                warn_style);
        }

        // Labels under the radar: distance, relative velocity (vs target), then class
        // (or kind for non-star targets).
        GUI.color = m_frame_color;
        string dist_str = FormatDistance(target_distance);
        GUI.Label(
            new Rect(cx - radius, cy + radius + 2f, radius * 2f, text_size * 1.4f),
            "DIST " + dist_str,
            label_style);
        GUI.Label(
            new Rect(cx - radius, cy + radius + 2f + text_size * 1.3f, radius * 2f, text_size * 1.4f),
            "VEL " + (CurrentTargetRelativeSpeed / 1000f).ToString("F2") + " ku/s",
            label_style);
        GUI.color = blip_color;
        string class_str = TargetIsStar
            ? "CLASS " + CurrentTargetClass.ToString()
            : m_ship.SelectedBody.kind.ToString().ToUpperInvariant();
        GUI.Label(
            new Rect(cx - radius, cy + radius + 2f + text_size * 2.6f, radius * 2f, text_size * 1.4f),
            class_str,
            label_style);

        // [SPACE] ENGAGE hint — only shown when not already engaged (top-right HUD covers
        // that case). Lit in class color when in range, dimmed "OUT OF RANGE" otherwise.
        OrbitAutopilot ap = m_ship.m_autopilot;
        if (ap != null && !ap.IsActive)
        {
            float hint_y = cy + radius + 2f + text_size * 3.9f;
            Rect hint_rect = new Rect(cx - radius, hint_y, radius * 2f, text_size * 1.4f);
            if (ap.CanEngage)
            {
                float engage_pulse = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2.5f) + 1f) * 0.5f;
                Color engage_color = blip_color;
                engage_color.a = Mathf.Lerp(0.7f, 1f, engage_pulse);
                GUI.color = engage_color;
                GUI.Label(hint_rect, "[SPACE] ENGAGE ORBIT", label_style);
            }
            else
            {
                GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
                GUI.Label(hint_rect, "[SPACE] OUT OF RANGE", label_style);
            }
        }

        GUI.color = prev;
    }

    void DrawModeBanner(float cx, float cy, float radius, float text_size, GUIStyle label_style)
    {
        if (m_ship == null) return;
        string text;
        Color color;
        switch (m_ship.CurrentTargetMode)
        {
            case ShipControl.TargetMode.AutoArrival:
                text = "[1] ARRIVAL"; color = new Color(1f, 0.8f, 0.4f, 1f); break;
            case ShipControl.TargetMode.AutoRefuel:
                text = "[2] REFUEL"; color = new Color(0.5f, 1f, 0.7f, 1f); break;
            default:
                text = "MANUAL"; color = m_frame_color; break;
        }
        Color prev = GUI.color;
        GUI.color = color;
        GUI.Label(
            new Rect(cx - radius, cy - radius - text_size * 1.6f, radius * 2f, text_size * 1.4f),
            text,
            label_style);
        GUI.color = prev;
    }

    static string FormatDistance(float d)
    {
        if (d < 1000f) return d.ToString("F0") + " u";
        if (d < 1_000_000f) return (d / 1000f).ToString("F1") + " ku";
        return (d / 1_000_000f).ToString("F2") + " Mu";
    }

    public static Color StarClassColor(StellarClass c)
    {
        switch (c)
        {
            case StellarClass.O: return new Color(0.55f, 0.65f, 1f, 1f);
            case StellarClass.B: return new Color(0.75f, 0.85f, 1f, 1f);
            case StellarClass.A: return new Color(1f, 1f, 1f, 1f);
            case StellarClass.F: return new Color(1f, 0.95f, 0.7f, 1f);
            case StellarClass.G: return new Color(1f, 0.9f, 0.55f, 1f);
            case StellarClass.K: return new Color(1f, 0.7f, 0.45f, 1f);
            case StellarClass.M: return new Color(1f, 0.5f, 0.4f, 1f);
        }
        return Color.white;
    }

    static void DrawDisc(float cx, float cy, float radius, Color color)
    {
        // Approximate a disc with horizontal scanlines (cheap, OnGUI-friendly).
        Color prev = GUI.color;
        GUI.color = color;
        int steps = Mathf.Max(12, Mathf.RoundToInt(radius));
        for (int i = -steps; i <= steps; i++)
        {
            float y_off = (i / (float)steps) * radius;
            float x_half = Mathf.Sqrt(Mathf.Max(0f, radius * radius - y_off * y_off));
            GUI.DrawTexture(new Rect(cx - x_half, cy + y_off, x_half * 2f, 1f), s_white_tex);
        }
        GUI.color = prev;
    }

    static void DrawRing(float cx, float cy, float radius, Color color, float thickness)
    {
        Color prev = GUI.color;
        GUI.color = color;
        int segments = Mathf.Max(24, Mathf.RoundToInt(radius * 0.5f));
        float prev_x = cx + radius;
        float prev_y = cy;
        for (int i = 1; i <= segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            float x = cx + Mathf.Cos(a) * radius;
            float y = cy + Mathf.Sin(a) * radius;
            DrawLineSegment(prev_x, prev_y, x, y, thickness);
            prev_x = x; prev_y = y;
        }
        GUI.color = prev;
    }

    static void DrawLineSegment(float x0, float y0, float x1, float y1, float thickness)
    {
        Matrix4x4 m = GUI.matrix;
        float dx = x1 - x0;
        float dy = y1 - y0;
        float len = Mathf.Sqrt(dx * dx + dy * dy);
        if (len < 1e-3f) return;
        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
        GUIUtility.RotateAroundPivot(angle, new Vector2(x0, y0));
        GUI.DrawTexture(new Rect(x0, y0 - thickness * 0.5f, len, thickness), s_white_tex);
        GUI.matrix = m;
    }

}
