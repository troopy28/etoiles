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

    private int m_id = -1;
    private bool m_registered = false;

    public int Id => m_id;

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
    }

    void OnDestroy()
    {
        if (!m_registered || m_manager == null) return;
        m_manager.UnregisterBody(m_id);
    }
}
