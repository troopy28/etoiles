using UnityEngine;
using Unity.Mathematics;

public class SimGravityBody : MonoBehaviour
{
    public SimGravityManager m_manager;
    public float3 m_initial_velocity;
    public float mass = 0.0f;

    private int m_id;
    public int Id => m_id;

    void Start()
    {
        m_id = m_manager.RegisterBody(transform.position, m_initial_velocity, mass);
    }

    void LateUpdate()
    {
        transform.position = m_manager.GetPosition(m_id);
    }

    void OnDestroy()
    {
        m_manager.UnregisterBody(m_id);
    }
}
