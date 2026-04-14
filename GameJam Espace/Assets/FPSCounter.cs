using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    private float m_delta_time;

    void Update()
    {
        m_delta_time += (Time.unscaledDeltaTime - m_delta_time) * 0.1f;
    }

    void OnGUI()
    {
        float fps = 1.0f / m_delta_time;
        float ms = m_delta_time * 1000.0f;
        string text = $"{fps:0.} fps ({ms:0.0} ms)";

        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;

        GUI.Label(new Rect(10, 10, 300, 40), text, style);
    }
}
