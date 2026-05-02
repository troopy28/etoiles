using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class PauseMenuManager : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject m_pausePanel;
    public CanvasGroup m_pauseCanvasGroup;
    
    [Header("Cinematic Settings")]
    public float m_transitionSpeed = 5f;
    public float m_orbitSpeed = 0.5f;
    public float m_orbitDistance = 15f;

    private bool m_isPaused = false;
    private ShipControl m_playerShip;
    private Camera m_mainCamera;
    
    private Vector3 m_originalCamLocalPos;
    private Quaternion m_originalCamLocalRot;
    private Transform m_cameraParent;
    private Coroutine m_cinematicRoutine;

    void Start()
    {
        if (m_pausePanel == null) CreateUI();
        else m_pausePanel.SetActive(false);

        if (m_pauseCanvasGroup) m_pauseCanvasGroup.alpha = 0f;
        
        m_playerShip = FindAnyObjectByType<ShipControl>();
        m_mainCamera = Camera.main;
        
        if (m_mainCamera != null)
        {
            m_cameraParent = m_mainCamera.transform.parent;
            m_originalCamLocalPos = m_mainCamera.transform.localPosition;
            m_originalCamLocalRot = m_mainCamera.transform.localRotation;
        }
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (m_isPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (m_isPaused) return;
        m_isPaused = true;
        
        Time.timeScale = 0f;
        
        if (m_pausePanel) m_pausePanel.SetActive(true);
        if (m_playerShip) m_playerShip.enabled = false;
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (m_cinematicRoutine != null) StopCoroutine(m_cinematicRoutine);
        m_cinematicRoutine = StartCoroutine(OrbitCinematicRoutine());
    }

    public void Resume()
    {
        if (!m_isPaused) return;
        m_isPaused = false;
        
        Time.timeScale = 1f;
        
        if (m_playerShip) m_playerShip.enabled = true;
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (m_cinematicRoutine != null) StopCoroutine(m_cinematicRoutine);
        m_cinematicRoutine = StartCoroutine(RestoreCameraRoutine());
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Menu");
    }

    private void CreateUI()
    {
        GameObject canvasObj = new GameObject("PauseCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.AddComponent<GraphicRaycaster>();

        m_pausePanel = new GameObject("PausePanel");
        m_pausePanel.transform.SetParent(canvasObj.transform, false);
        m_pauseCanvasGroup = m_pausePanel.AddComponent<CanvasGroup>();
        
        // Background
        Image bg = m_pausePanel.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.6f);
        RectTransform rt = bg.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        // Content
        GameObject content = new GameObject("Content");
        content.transform.SetParent(m_pausePanel.transform, false);
        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 20;
        vlg.childControlHeight = false; vlg.childControlWidth = false;

        // Title
        CreateText(content.transform, "PAUSE", 80, Color.cyan);
        
        // Buttons
        CreateButton(content.transform, "RESUME", () => Resume());
        CreateButton(content.transform, "MAIN MENU", () => QuitToMenu());
        
        m_pausePanel.SetActive(false);
    }

    private void CreateText(Transform parent, string txt, int size, Color color)
    {
        GameObject g = new GameObject("Text");
        g.transform.SetParent(parent, false);
        Text t = g.AddComponent<Text>();
        t.text = txt;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        g.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 100);
    }

    private void CreateButton(Transform parent, string label, System.Action onClick)
    {
        GameObject g = new GameObject("Button_" + label);
        g.transform.SetParent(parent, false);
        Image img = g.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        Button b = g.AddComponent<Button>();
        b.onClick.AddListener(() => onClick());
        
        RectTransform rt = g.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300, 60);

        GameObject tObj = new GameObject("Label");
        tObj.transform.SetParent(g.transform, false);
        Text t = tObj.AddComponent<Text>();
        t.text = label;
        t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = 24;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        tObj.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 60);
    }

    private IEnumerator OrbitCinematicRoutine()
    {
        float t = 0;
        Vector3 startPos = m_mainCamera.transform.position;
        Quaternion startRot = m_mainCamera.transform.rotation;
        
        // Trouver le corps céleste le plus proche pour l'arrière-plan
        Transform targetCelestial = null;
        float minCDist = float.MaxValue;
        foreach (var body in FindObjectsByType<SimGravityBody>(FindObjectsInactive.Exclude))
        {
            if (body.gameObject == m_playerShip.gameObject) continue;
            float d = Vector3.Distance(m_playerShip.transform.position, body.transform.position);
            if (d < minCDist) { minCDist = d; targetCelestial = body.transform; }
        }

        while (m_isPaused)
        {
            t += Time.unscaledDeltaTime;
            
            // Fade UI
            if (m_pauseCanvasGroup != null)
                m_pauseCanvasGroup.alpha = Mathf.Clamp01(t * 2f);

            // Calcul de la position orbitale cinématique
            float angle = t * m_orbitSpeed;
            Vector3 orbitDir = new Vector3(Mathf.Cos(angle), 0.2f, Mathf.Sin(angle)).normalized;
            
            // On veut cadrer le vaisseau avec le corps céleste en fond si possible
            Vector3 lookDir = (m_playerShip.transform.position - (targetCelestial != null ? targetCelestial.position : Vector3.zero)).normalized;
            if (targetCelestial == null) lookDir = m_playerShip.transform.forward;
            
            // Position de la caméra "Cinématique"
            // On se place "entre" le vaisseau et le corps céleste pour avoir les deux dans le champ
            Vector3 backVec = (targetCelestial != null) ? (m_playerShip.transform.position - targetCelestial.position).normalized : m_playerShip.transform.forward;
            Vector3 sideVec = Vector3.Cross(backVec, Vector3.up).normalized;
            
            Vector3 targetCamPos = m_playerShip.transform.position + (sideVec * Mathf.Cos(angle) + Vector3.up * 0.2f + backVec * Mathf.Sin(angle)) * m_orbitDistance;
            
            // Transition douce vers la vue orbite
            float transitionT = Mathf.Clamp01(t * m_transitionSpeed * 0.4f);
            m_mainCamera.transform.position = Vector3.Lerp(startPos, targetCamPos, transitionT);
            
            // Regarder vers le vaisseau
            Quaternion targetRot = Quaternion.LookRotation(m_playerShip.transform.position - m_mainCamera.transform.position);
            m_mainCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, transitionT);
            
            yield return null;
        }
    }

    private IEnumerator RestoreCameraRoutine()
    {
        float t = 0;
        Vector3 startPos = m_mainCamera.transform.position;
        Quaternion startRot = m_mainCamera.transform.rotation;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * m_transitionSpeed;
            float smoothT = Mathf.SmoothStep(0, 1, t);
            
            if (m_pauseCanvasGroup != null)
                m_pauseCanvasGroup.alpha = 1f - smoothT;

            // Revenir à la position locale originale par rapport au parent (vaisseau)
            if (m_cameraParent != null)
            {
                m_mainCamera.transform.position = Vector3.Lerp(startPos, m_cameraParent.TransformPoint(m_originalCamLocalPos), smoothT);
                m_mainCamera.transform.rotation = Quaternion.Slerp(startRot, m_cameraParent.rotation * m_originalCamLocalRot, smoothT);
            }
            else
            {
                // Fallback si pas de parent
                m_mainCamera.transform.position = Vector3.Lerp(startPos, m_originalCamLocalPos, smoothT);
                m_mainCamera.transform.rotation = Quaternion.Slerp(startRot, m_originalCamLocalRot, smoothT);
            }
            
            yield return null;
        }
        
        // Remise à zéro propre
        m_mainCamera.transform.SetParent(m_cameraParent);
        m_mainCamera.transform.localPosition = m_originalCamLocalPos;
        m_mainCamera.transform.localRotation = m_originalCamLocalRot;
        if (m_pausePanel) m_pausePanel.SetActive(false);
    }
}
