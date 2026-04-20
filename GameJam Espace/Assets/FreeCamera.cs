using UnityEngine;
using UnityEngine.InputSystem;

public class FreeCamera : MonoBehaviour
{
    public float m_move_speed = 10f;
    public float m_fast_multiplier = 3f;
    public float m_sensitivity = 2f;

    private float m_yaw;
    private float m_pitch;

    void Start()
    {
        Vector3 euler = transform.eulerAngles;
        m_yaw = euler.y;
        m_pitch = euler.x;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        // Unlock cursor with Escape
        if (kb.escapeKey.wasPressedThisFrame)
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked
                ? CursorLockMode.None
                : CursorLockMode.Locked;

        // Mouse look
        Vector2 mouseDelta = mouse.delta.ReadValue();
        m_yaw += mouseDelta.x * m_sensitivity * 0.1f;
        m_pitch -= mouseDelta.y * m_sensitivity * 0.1f;
        m_pitch = Mathf.Clamp(m_pitch, -90f, 90f);
        transform.rotation = Quaternion.Euler(m_pitch, m_yaw, 0f);

        // Movement
        float speed = m_move_speed;
        if (kb.leftShiftKey.isPressed)
            speed *= m_fast_multiplier;

        Vector3 dir = Vector3.zero;
        // Input System uses PHYSICAL QWERTY positions. On AZERTY:
        // Z→W, Q→A, A→Q (physical positions).
        if (kb.wKey.isPressed) dir += transform.forward;   // AZERTY Z
        if (kb.sKey.isPressed) dir -= transform.forward;   // AZERTY S
        if (kb.dKey.isPressed) dir += transform.right;     // AZERTY D
        if (kb.aKey.isPressed) dir -= transform.right;     // AZERTY Q
        if (kb.eKey.isPressed) dir += Vector3.up;          // AZERTY E
        if (kb.qKey.isPressed) dir -= Vector3.up;          // AZERTY A

        transform.position += dir.normalized * speed * Time.deltaTime;
    }
}
