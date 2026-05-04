using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

// Watches the player's ShipControl for overheat (m_temperature >= m_temperature_max) OR
// fuel exhaustion (m_fuel <= 0, only when not actively refueling — the latter avoids a
// spurious trigger on the first tick before the proximity refuel check has run).
// On trigger: freezes the game, displays a full-screen "GAME OVER" panel, then loads the
// Menu scene after m_display_duration seconds. One-shot — the ship script is disabled
// during the delay so the player can't keep flying / refueling out of it.
public class GameOverManager : MonoBehaviour
{
    [Header("Refs")]
    public ShipControl m_ship;            // auto-found if null
    public string m_menu_scene_name = "MainMenu";

    [Header("Display")]
    public float m_display_duration = 3f;
    public Color m_text_color = new Color(1f, 0.15f, 0.1f, 1f);
    public int m_text_size = 140;

    private bool m_triggered = false;
    private GameObject m_panel;
    private Text m_text;
    private CanvasGroup m_canvas_group;

    // Auto-spawn one instance per loaded scene if none exists (prevents the user from
    // forgetting to drop the component in every gameplay scene). RuntimeInitializeOnLoadMethod
    // only fires once at app startup, so we hook SceneManager.sceneLoaded to catch every
    // subsequent scene transition (Menu → Galaxie, etc.).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;   // idempotent across domain reloads
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
    {
        EnsureInstance();
    }

    static void EnsureInstance()
    {
        if (FindAnyObjectByType<GameOverManager>() != null) return;
        if (FindAnyObjectByType<ShipControl>() == null) return;
        var go = new GameObject("GameOverManager (auto)");
        go.AddComponent<GameOverManager>();
    }

    void Start()
    {
        if (m_ship == null) m_ship = FindAnyObjectByType<ShipControl>();
        CreateUI();
    }

    void Update()
    {
        if (m_triggered) return;
        if (m_ship == null)
        {
            // Try to re-acquire if a ship spawns later than this manager.
            m_ship = FindAnyObjectByType<ShipControl>();
            return;
        }

        bool overheated = m_ship.IsOverheating;
        bool fuel_empty = m_ship.m_fuel <= 0f && !m_ship.m_infinite_fuel && !m_ship.IsRefueling;
        if (overheated || fuel_empty)
        {
            m_triggered = true;
            if (m_text)
                m_text.text = overheated ? "GAME OVER\nSHIP OVERHEATED" : "GAME OVER\nFUEL DEPLETED";
            StartCoroutine(GameOverRoutine());
        }
    }

    IEnumerator GameOverRoutine()
    {
        if (m_ship) m_ship.enabled = false;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (m_panel) m_panel.SetActive(true);

        // Fade in the panel over ~0.4 s of unscaled time so it doesn't pop.
        float t = 0f;
        const float fadeIn = 0.4f;
        while (t < fadeIn)
        {
            t += Time.unscaledDeltaTime;
            if (m_canvas_group) m_canvas_group.alpha = Mathf.Clamp01(t / fadeIn);
            yield return null;
        }
        if (m_canvas_group) m_canvas_group.alpha = 1f;

        // Hold "GAME OVER" on screen.
        yield return new WaitForSecondsRealtime(m_display_duration);

        Time.timeScale = 1f;
        SceneManager.LoadScene(m_menu_scene_name);
    }

    void CreateUI()
    {
        var canvasObj = new GameObject("GameOverCanvas");
        canvasObj.transform.SetParent(transform, false);
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;   // above pause menu
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        m_panel = new GameObject("GameOverPanel");
        m_panel.transform.SetParent(canvasObj.transform, false);
        m_canvas_group = m_panel.AddComponent<CanvasGroup>();

        var bg = m_panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);
        var rt = bg.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        // Centered "GAME OVER" text
        var textObj = new GameObject("GameOverText");
        textObj.transform.SetParent(m_panel.transform, false);
        m_text = textObj.AddComponent<Text>();
        m_text.text = "GAME OVER";
        m_text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        m_text.fontSize = m_text_size;
        m_text.fontStyle = FontStyle.Bold;
        m_text.color = m_text_color;
        m_text.alignment = TextAnchor.MiddleCenter;
        var textRt = m_text.rectTransform;
        textRt.anchorMin = new Vector2(0.5f, 0.5f);
        textRt.anchorMax = new Vector2(0.5f, 0.5f);
        textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.anchoredPosition = Vector2.zero;
        textRt.sizeDelta = new Vector2(1600, 300);

        m_canvas_group.alpha = 0f;
        m_panel.SetActive(false);
    }
}
