using UnityEngine;
using Unity.Mathematics;
using Assets.Code.Scripts.Generation;

// Mini-radar 2D circulaire (bottom-left HUD). Locks onto the closest refuel-compatible
// star (class O/B/A/F) by querying SimGravityManager every refresh_interval.
// Renders the target as a blip projected on the ship's local horizontal plane.
public class RadarHUD : MonoBehaviour
{
    [Header("Refs")]
    public ShipControl m_ship;                // auto-found if null
    public Camera m_camera;                   // auto-found via Camera.main if null — defines the radar's "forward" axis

    [Header("Tracking")]
    public float m_refresh_interval = 0.1f;   // target refresh rate (seconds)

    [Header("Visuals")]
    [Range(0.05f, 0.2f)] public float m_radius_ratio = 0.09f;   // radar radius as fraction of screen height
    public Color m_frame_color = new Color(0.4f, 0.9f, 1f, 0.85f);
    public Color m_no_target_color = new Color(0.6f, 0.6f, 0.6f, 0.7f);

    public int CurrentTargetId => m_target_id;
    public bool HasTarget => m_target_id >= 0;
    public float CurrentTargetDistance => m_target_distance;
    public Vector3 CurrentTargetPos => m_target_pos;
    public StellarClass CurrentTargetClass => m_target_class;

    private int m_target_id = -1;
    private float m_target_distance = 0f;
    private Vector3 m_target_pos = Vector3.zero;
    private StellarClass m_target_class = StellarClass.O;
    private float m_next_refresh = 0f;
    private static Texture2D s_white_tex;

    void Start()
    {
        if (m_ship == null) m_ship = GetComponent<ShipControl>();
        if (m_camera == null) m_camera = Camera.main;
    }

    void Update()
    {
        if (m_ship == null) return;
        if (Time.unscaledTime < m_next_refresh) return;
        m_next_refresh = Time.unscaledTime + m_refresh_interval;
        RefreshTarget();
    }

    void LateUpdate()
    {
        // Keep the cached target position in sync with the simulation each frame so the
        // blip tracks the star smoothly between target refreshes (otherwise the blip
        // would be stale up to refresh_interval seconds).
        if (m_target_id < 0) return;
        var mgr = (m_ship != null && m_ship.m_gravity_body != null) ? m_ship.m_gravity_body.m_manager : null;
        if (mgr == null || !mgr.m_curr.IsCreated || m_target_id >= mgr.m_curr.Length) return;
        double4 cur = mgr.m_curr[m_target_id];
        if (cur.w == 0.0) { m_target_id = -1; return; }   // target was unregistered
        m_target_pos = (Vector3)(float3)cur.xyz;
        m_target_distance = Vector3.Distance(m_ship.transform.position, m_target_pos);
    }

    void RefreshTarget()
    {
        var mgr = (m_ship.m_gravity_body != null) ? m_ship.m_gravity_body.m_manager : null;
        if (mgr == null) { m_target_id = -1; return; }
        float3 from = m_ship.transform.position;
        if (!mgr.TryFindClosestRefuelStar(from, out int id, out float dist))
        {
            m_target_id = -1;
            return;
        }
        m_target_id = id;
        m_target_distance = dist;
        m_target_pos = (Vector3)(float3)mgr.m_curr[id].xyz;
        m_target_class = (StellarClass)mgr.m_stellar_class[id];
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

        if (m_target_id < 0)
        {
            GUI.color = m_no_target_color;
            GUI.Label(
                new Rect(cx - radius, cy + radius + 2f, radius * 2f, text_size * 1.4f),
                "NO REFUEL TARGET",
                label_style);
            GUI.color = prev;
            return;
        }

        // Forward-cone polar projection: angular distance from the camera's forward axis
        // maps to the blip's distance from the radar center. Center = target dead ahead;
        // inner ring (0.5×radius) = perpendicular (90°); rim = dead behind.
        // Using the camera frame (rather than the ship transform) means free-look and the
        // soft-follow lag are honoured, so the radar always matches what the player sees.
        // Fallback to the ship's logical frame (Z forward, Y up) if no camera is bound;
        // never use raw transform.InverseTransformDirection since the FBX has a -90/-90/0
        // rest rotation that would skew the projection.
        Vector3 world_offset = m_target_pos - m_ship.transform.position;
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
        Color blip_color = StarClassColor(m_target_class);
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

        // Labels under the radar: distance + class
        GUI.color = m_frame_color;
        string dist_str = FormatDistance(m_target_distance);
        GUI.Label(
            new Rect(cx - radius, cy + radius + 2f, radius * 2f, text_size * 1.4f),
            "DIST " + dist_str,
            label_style);
        GUI.color = blip_color;
        GUI.Label(
            new Rect(cx - radius, cy + radius + 2f + text_size * 1.3f, radius * 2f, text_size * 1.4f),
            "CLASS " + m_target_class.ToString(),
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
