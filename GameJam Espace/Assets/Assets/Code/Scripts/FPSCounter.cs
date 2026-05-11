using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    private static FPSCounter m_instance;
    private float m_delta_time;
    private bool m_visible = false;

    void Awake()
    {
        if (m_instance != null) { Destroy(gameObject); return; }
        m_instance = this;
        DontDestroyOnLoad(gameObject);
        
        m_visible = PlayerPrefs.GetInt("ShowFPS", 0) == 1;
    }

    public static void SetVisible(bool visible)
    {
        if (m_instance == null)
        {
            GameObject go = new GameObject("FPSManager");
            go.AddComponent<FPSCounter>();
        }
        
        m_instance.m_visible = visible;
        PlayerPrefs.SetInt("ShowFPS", visible ? 1 : 0);
    }

    void Update()
    {
        if (!m_visible) return;
        m_delta_time += (Time.unscaledDeltaTime - m_delta_time) * 0.1f;
    }

    void OnGUI()
    {
        if (!m_visible) return;
        
        float fps = 1.0f / m_delta_time;
        float ms = m_delta_time * 1000.0f;
        string text = $"{fps:0.} fps ({ms:0.0} ms)";

        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = fps < 30 ? Color.red : (fps < 60 ? Color.yellow : Color.white);

        GUI.Label(new Rect(10, 10, 300, 40), text, style);
    }
}
