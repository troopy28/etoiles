using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Assets.Code.Scripts.Generation;

public enum BodyKind
{
    Other,
    Star,
    Planet,
    Moon,
    Asteroid,
    Comet,
    Wreck
}

public class SimGravityBody : MonoBehaviour
{
    public SimGravityManager m_manager;
    public float3 m_initial_velocity;
    public float mass = 0.0f;

    [Header("Classification")]
    public BodyKind kind = BodyKind.Other;
    public StellarClass spectral_class;       // only meaningful when kind == Star
    public bool is_destination = false;       // true for the mission's POINT_B_END star
    public bool is_start_point = false;       // true for the mission's starting position (immune to collision)

    [Header("Visuals")]
    public float visual_radius = 0.5f;        // world-space radius of the visible surface; used by HUD distance and proximity checks
    public float3 m_spin_axis = new float3(0f, 1f, 0f);
    public float m_spin_speed = 0f;           // rad/s; zero disables rotation in the simulation job

    private int m_id = -1;
    private bool m_registered = false;

    public int Id => m_id;

    private static readonly List<SimGravityBody> s_all = new List<SimGravityBody>();
    public static IReadOnlyList<SimGravityBody> AllRegistered => s_all;

    void Start()
    {
        if (m_manager == null)
            m_manager = FindAnyObjectByType<SimGravityManager>();
        if (m_manager == null)
        {
            Debug.LogError($"[SimGravityBody] No SimGravityManager found in scene for '{name}'. Body will not be simulated.", this);
            return;
        }
        m_id = m_manager.RegisterBody(this);
        m_registered = true;
        s_all.Add(this);
    }

    void OnDestroy()
    {
        if (!m_registered || m_manager == null) return;
        s_all.Remove(this);
        m_manager.UnregisterBody(m_id);
    }
}
