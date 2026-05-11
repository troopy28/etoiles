using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class PauseMenuManager : MonoBehaviour
{
	[Header("UI Panel (optionnel — créé automatiquement si null)")]
	public GameObject m_pausePanel;
	public CanvasGroup m_pauseCanvasGroup;

	[Header("Cinématique")]
	public float m_transitionSpeed = 4f;
	public float m_orbitSpeed = 0.3f;
	public float m_orbitDistance = 20f;

	private bool m_isPaused = false;
	public bool IsPaused => m_isPaused;

	private ShipControl m_playerShip;
	private Camera m_mainCamera;

	private Vector3 m_originalCamLocalPos;
	private Quaternion m_originalCamLocalRot;
	private Transform m_cameraParent;
	private Coroutine m_cinematicRoutine;

	private GameObject m_mainPausePanel;
	private GameObject m_confirmQuitPanel;
	private GameObject m_settingsPanel;
	
	private Dictionary<string, GameObject> m_settingsTabPanels = new Dictionary<string, GameObject>();
	private string m_currentTab = "GRAPHICS";
	private bool m_isSettingsOpen = false;

	void Start()
	{
		InputRemapper.Load();
		m_playerShip = FindAnyObjectByType<ShipControl>();
		m_mainCamera = Camera.main;

		if (m_mainCamera != null)
		{
			m_cameraParent = m_mainCamera.transform.parent;
			m_originalCamLocalPos = m_mainCamera.transform.localPosition;
			m_originalCamLocalRot = m_mainCamera.transform.localRotation;
		}

		if (m_pausePanel == null)
			CreatePauseUI();
		else
			m_pausePanel.SetActive(false);

		if (m_pauseCanvasGroup != null) m_pauseCanvasGroup.alpha = 0f;
	}

	void Update()
	{
		if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
		{
			if (GameProgressManager.Instance != null && GameProgressManager.Instance.IsGameEnded) return;
			if (GameProgressManager.Instance != null && GameProgressManager.Instance.IsBriefingActive) return;
			
			if (m_isSettingsOpen) CloseOptions();
			else TogglePause();
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
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;

		if (m_playerShip != null) m_playerShip.enabled = false;

		if (m_mainPausePanel != null)  m_mainPausePanel.SetActive(true);
		if (m_confirmQuitPanel != null) m_confirmQuitPanel.SetActive(false);
		if (m_settingsPanel != null) m_settingsPanel.SetActive(false);
		m_isSettingsOpen = false;

		if (m_pausePanel != null) m_pausePanel.SetActive(true);
		if (m_pauseCanvasGroup != null) m_pauseCanvasGroup.alpha = 0f;

		if (m_cinematicRoutine != null) StopCoroutine(m_cinematicRoutine);
		m_cinematicRoutine = StartCoroutine(FadeInRoutine());

		StartCoroutine(OrbitCinematicRoutine());
	}

	public void Resume()
	{
		if (!m_isPaused) return;
		m_isPaused = false;

		Time.timeScale = 1f;
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;

		if (m_playerShip != null) m_playerShip.enabled = true;

		if (m_cinematicRoutine != null) StopCoroutine(m_cinematicRoutine);
		m_cinematicRoutine = StartCoroutine(RestoreCameraRoutine());
	}

	public void Restart()
	{
		Time.timeScale = 1f;
		SceneManager.LoadScene(SceneManager.GetActiveScene().name);
	}

	public void QuitToMenu()
	{
		if (m_confirmQuitPanel != null)
			StartCoroutine(SwitchPanelRoutine(m_mainPausePanel, m_confirmQuitPanel));
		else
		{
			Time.timeScale = 1f;
			SceneManager.LoadScene("MainMenu");
		}
	}

	public void ConfirmQuit()
	{
		Time.timeScale = 1f;
		SceneManager.LoadScene("MainMenu");
	}

	public void CancelQuit()
	{
		StartCoroutine(SwitchPanelRoutine(m_confirmQuitPanel, m_mainPausePanel));
	}

	public void OpenOptions()
	{
		StartCoroutine(SwitchPanelRoutine(m_mainPausePanel, m_settingsPanel));
		m_isSettingsOpen = true;
	}

	public void CloseOptions()
	{
		StartCoroutine(SwitchPanelRoutine(m_settingsPanel, m_mainPausePanel));
		m_isSettingsOpen = false;
	}

	private IEnumerator SwitchPanelRoutine(GameObject from, GameObject to)
	{
		CanvasGroup fromGroup = from?.GetComponent<CanvasGroup>();
		CanvasGroup toGroup = to?.GetComponent<CanvasGroup>();

		if (fromGroup != null)
		{
			float t = 1f;
			while (t > 0f)
			{
				t -= Time.unscaledDeltaTime * 10f;
				fromGroup.alpha = Mathf.Clamp01(t);
				yield return null;
			}
		}
		
		if (from != null) from.SetActive(false);
		if (to != null) to.SetActive(true);

		if (toGroup != null)
		{
			float t = 0f;
			toGroup.alpha = 0f;
			while (t < 1f)
			{
				t += Time.unscaledDeltaTime * 10f;
				toGroup.alpha = Mathf.Clamp01(t);
				yield return null;
			}
		}
	}


	private IEnumerator FadeInRoutine()
	{
		float t = 0f;
		while (t < 1f)
		{
			t += Time.unscaledDeltaTime * m_transitionSpeed;
			if (m_pauseCanvasGroup != null)
				m_pauseCanvasGroup.alpha = Mathf.Clamp01(t);
			yield return null;
		}
	}

	private IEnumerator OrbitCinematicRoutine()
	{
		if (m_mainCamera == null || m_playerShip == null) yield break;

		float t = 0f;
		Vector3 startPos = m_mainCamera.transform.position;
		Quaternion startRot = m_mainCamera.transform.rotation;

		Transform targetCelestial = null;
		float minDist = float.MaxValue;
		foreach (var body in FindObjectsByType<SimGravityBody>(FindObjectsInactive.Exclude))
		{
			if (body == null) continue;
			if (m_playerShip != null && body.gameObject == m_playerShip.gameObject) continue;
			float d = Vector3.Distance(m_playerShip.transform.position, body.transform.position);
			if (d < minDist) { minDist = d; targetCelestial = body.transform; }
		}

		while (m_isPaused)
		{
			t += Time.unscaledDeltaTime;

			float angle = t * m_orbitSpeed;
			Vector3 backVec = (targetCelestial != null)
				? (m_playerShip.transform.position - targetCelestial.position).normalized
				: m_playerShip.transform.forward;
			Vector3 sideVec = Vector3.Cross(backVec, Vector3.up).normalized;

			Vector3 targetCamPos = m_playerShip.transform.position
				+ (sideVec * Mathf.Cos(angle) + Vector3.up * 0.25f + backVec * Mathf.Sin(angle))
				* m_orbitDistance;

			float transT = Mathf.Clamp01(t * m_transitionSpeed * 0.3f);
			m_mainCamera.transform.position = Vector3.Lerp(startPos, targetCamPos, transT);

			Quaternion targetRot = Quaternion.LookRotation(m_playerShip.transform.position - m_mainCamera.transform.position);
			m_mainCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, transT);

			if (transT >= 1f)
			{
				startPos = targetCamPos;
				startRot = targetRot;
			}

			yield return null;
		}
	}

	private IEnumerator RestoreCameraRoutine()
	{
		if (m_mainCamera == null) yield break;

		if (m_pauseCanvasGroup != null)
		{
			float ft = 1f;
			while (ft > 0f)
			{
				ft -= Time.unscaledDeltaTime * m_transitionSpeed * 2f;
				m_pauseCanvasGroup.alpha = Mathf.Clamp01(ft);
				yield return null;
			}
		}

		if (m_pausePanel != null) m_pausePanel.SetActive(false);

		float t = 0f;
		Vector3 startPos = m_mainCamera.transform.position;
		Quaternion startRot = m_mainCamera.transform.rotation;

		while (t < 1f)
		{
			t += Time.unscaledDeltaTime * m_transitionSpeed;
			float smoothT = Mathf.SmoothStep(0, 1, t);

			if (m_cameraParent != null)
			{
				m_mainCamera.transform.position = Vector3.Lerp(startPos, m_cameraParent.TransformPoint(m_originalCamLocalPos), smoothT);
				m_mainCamera.transform.rotation = Quaternion.Slerp(startRot, m_cameraParent.rotation * m_originalCamLocalRot, smoothT);
			}
			else
			{
				m_mainCamera.transform.position = Vector3.Lerp(startPos, m_originalCamLocalPos, smoothT);
				m_mainCamera.transform.rotation = Quaternion.Slerp(startRot, m_originalCamLocalRot, smoothT);
			}

			yield return null;
		}

		if (m_cameraParent != null)
		{
			m_mainCamera.transform.SetParent(m_cameraParent);
			m_mainCamera.transform.localPosition = m_originalCamLocalPos;
			m_mainCamera.transform.localRotation = m_originalCamLocalRot;
		}
	}


	private void CreatePauseUI()
	{
		GameObject canvasObj = new GameObject("PauseCanvas", typeof(RectTransform));
		Canvas canvas = canvasObj.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 200;
		CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);
		scaler.matchWidthOrHeight = 0.5f;
		canvasObj.AddComponent<GraphicRaycaster>();

		if (FindAnyObjectByType<EventSystem>() == null)
		{
			GameObject es = new GameObject("EventSystem");
			es.AddComponent<EventSystem>();
			es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
		}

		m_pausePanel = CreatePanel(canvasObj.transform, "PausePanel", new Color(0, 0, 0, 0f));
		m_pauseCanvasGroup = m_pausePanel.AddComponent<CanvasGroup>();

		GameObject bgOverlay = new GameObject("BgOverlay", typeof(RectTransform));
		bgOverlay.transform.SetParent(m_pausePanel.transform, false);
		Image bgImg = bgOverlay.AddComponent<Image>();
		bgImg.color = new Color(0.02f, 0.02f, 0.05f, 0.95f);
		StretchFull(bgImg.rectTransform);

		CreateSideBar(m_pausePanel.transform);

		m_mainPausePanel = new GameObject("MainPausePanel", typeof(RectTransform));
		m_mainPausePanel.transform.SetParent(m_pausePanel.transform, false);
		RectTransform mainRT = m_mainPausePanel.GetComponent<RectTransform>();
		mainRT.anchorMin = new Vector2(0.5f, 0.5f);
		mainRT.anchorMax = new Vector2(0.5f, 0.5f);
		mainRT.pivot = new Vector2(0.5f, 0.5f);
		mainRT.anchoredPosition = new Vector2(100, 0);
		mainRT.sizeDelta = new Vector2(500, 720);

		m_mainPausePanel.AddComponent<CanvasGroup>();

		Image cardBg = m_mainPausePanel.AddComponent<Image>();
		cardBg.color = new Color(0, 0, 0, 0.98f);
		
		Shader scifiShader = Shader.Find("UI/SciFiPanel");
		if (scifiShader != null) cardBg.material = new Material(scifiShader);
		
		CreateOutlineGlow(m_mainPausePanel.transform, new Color(0f, 0.8f, 1f, 0.7f));

		VerticalLayoutGroup vlg = m_mainPausePanel.AddComponent<VerticalLayoutGroup>();
		vlg.childAlignment = TextAnchor.MiddleCenter;
		vlg.spacing = 35f;
		vlg.padding = new RectOffset(40, 40, 60, 60);
		vlg.childControlWidth = true;
		vlg.childControlHeight = true;
		vlg.childForceExpandWidth = true;
		vlg.childForceExpandHeight = false;

		CreateTitleLabel(m_mainPausePanel.transform, "SYSTEM PAUSED", 58, new Color(0f, 0.8f, 1f));
		CreateSeparatorLine(m_mainPausePanel.transform, new Color(0f, 0.8f, 1f, 0.6f));

		CreateSubtitleLabel(m_mainPausePanel.transform, "> MISSION ON HOLD", new Color(0.7f, 0.9f, 1f, 0.7f));

		CreateMenuButton(m_mainPausePanel.transform, "REPRENDRE",         "RESUME MISSION",     Resume,    new Color(0f, 0.8f, 1f, 0.05f), new Color(0f, 0.8f, 1f, 0.4f));
		CreateMenuButton(m_mainPausePanel.transform, "OPTIONS",           "SYSTEM SETTINGS",    OpenOptions,new Color(0f, 0.8f, 1f, 0.05f), new Color(0f, 0.8f, 1f, 0.4f));
		CreateMenuButton(m_mainPausePanel.transform, "RECOMMENCER",       "RESTART MISSION",    Restart,   new Color(0f, 0.8f, 1f, 0.05f), new Color(0f, 0.8f, 1f, 0.4f));
		CreateMenuButton(m_mainPausePanel.transform, "MENU PRINCIPAL",    "ABANDON MISSION",    QuitToMenu,new Color(1f, 0.2f, 0.2f, 0.05f), new Color(1f, 0.3f, 0.3f, 0.4f));

		m_settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform));
		m_settingsPanel.transform.SetParent(m_pausePanel.transform, false);
		RectTransform setRT = m_settingsPanel.GetComponent<RectTransform>();
		setRT.anchorMin = new Vector2(0.5f, 0.5f);
		setRT.anchorMax = new Vector2(0.5f, 0.5f);
		setRT.pivot = new Vector2(0.5f, 0.5f);
		setRT.anchoredPosition = new Vector2(100, 0);
		setRT.sizeDelta = new Vector2(900, 750);

		m_settingsPanel.AddComponent<CanvasGroup>();

		Image setBg = m_settingsPanel.AddComponent<Image>();
		setBg.color = new Color(0, 0, 0, 0.98f);
		if (scifiShader != null) setBg.material = new Material(scifiShader);
		CreateOutlineGlow(m_settingsPanel.transform, new Color(0f, 0.8f, 1f, 0.6f));

		GameObject horizontalRoot = new GameObject("HorizontalRoot", typeof(RectTransform));
		horizontalRoot.transform.SetParent(m_settingsPanel.transform, false);
		StretchFull(horizontalRoot.GetComponent<RectTransform>());
		
		HorizontalLayoutGroup hlg = horizontalRoot.AddComponent<HorizontalLayoutGroup>();
		hlg.childControlWidth = true; hlg.childControlHeight = true;
		hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;

		GameObject sidebar = new GameObject("SettingsSidebar", typeof(RectTransform));
		sidebar.transform.SetParent(horizontalRoot.transform, false);
		LayoutElement sLe = sidebar.AddComponent<LayoutElement>();
		sLe.preferredWidth = 220;
		sLe.flexibleWidth = 0;
		
		Image sImg = sidebar.AddComponent<Image>();
		sImg.color = new Color(0, 0, 0, 0.4f);

		VerticalLayoutGroup sVlg = sidebar.AddComponent<VerticalLayoutGroup>();
		sVlg.padding = new RectOffset(25, 25, 40, 40);
		sVlg.spacing = 15;
		sVlg.childAlignment = TextAnchor.UpperCenter;
		sVlg.childControlWidth = true; sVlg.childControlHeight = false;
		sVlg.childForceExpandHeight = false;

		CreateTitleLabel(sidebar.transform, "SYSTEM", 32, new Color(0f, 0.8f, 1f));
		CreateSeparatorLine(sidebar.transform, new Color(0f, 0.8f, 1f, 0.3f));

		CreateTabButton(sidebar.transform, "GRAPHICS", "GRAPHICS", () => SwitchSettingsTab("GRAPHICS"));
		CreateTabButton(sidebar.transform, "AUDIO",    "AUDIO",    () => SwitchSettingsTab("AUDIO"));
		CreateTabButton(sidebar.transform, "GAMEPLAY", "GAMEPLAY", () => SwitchSettingsTab("GAMEPLAY"));
		CreateTabButton(sidebar.transform, "CONTROLS", "CONTROLS", () => SwitchSettingsTab("CONTROLS"));
		
		GameObject spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
		CreateMenuButton(sidebar.transform, "RETOUR", "BACK", CloseOptions, new Color(0f, 0.8f, 1f, 0.1f), new Color(0f, 0.8f, 1f, 0.4f));
		GameObject btnRet = sidebar.transform.Find("Btn_RETOUR").gameObject;
		LayoutElement reLE = btnRet.AddComponent<LayoutElement>();
		reLE.ignoreLayout = true;
		RectTransform reRT = btnRet.GetComponent<RectTransform>();
		reRT.anchorMin = new Vector2(0.5f, 0); reRT.anchorMax = new Vector2(0.5f, 0);
		reRT.pivot = new Vector2(0.5f, 0);
		reRT.anchoredPosition = new Vector2(0, 45);
		reRT.sizeDelta = new Vector2(170, 40);

		GameObject contentArea = new GameObject("ContentArea", typeof(RectTransform));
		contentArea.transform.SetParent(horizontalRoot.transform, false);
		contentArea.AddComponent<LayoutElement>().flexibleWidth = 1;

		SettingsManager sm = FindAnyObjectByType<SettingsManager>();
		if (sm == null) {
			sm = gameObject.AddComponent<SettingsManager>();
			sm.m_playerShip = m_playerShip;
		}

		m_settingsTabPanels["GRAPHICS"] = CreateSettingsTabPanel(contentArea.transform, "GraphicsContent");
		m_settingsTabPanels["AUDIO"]    = CreateSettingsTabPanel(contentArea.transform, "AudioContent");
		m_settingsTabPanels["GAMEPLAY"] = CreateSettingsTabPanel(contentArea.transform, "GameplayContent");

		GameObject controlsContent = CreateSettingsTabPanel(contentArea.transform, "ControlsContent", true);
		m_settingsTabPanels["CONTROLS"] = controlsContent.transform.parent.parent.gameObject; 

		if (sm != null)
		{
			Transform g = m_settingsTabPanels["GRAPHICS"].transform;
			CreateTitleLabel(g, "GRAPHICS PROTOCOL", 28, new Color(0f, 0.8f, 1f));
			CreateOptionToggle(g, "FULLSCREEN", "FULLSCREEN", (v) => sm.SetFullscreen(v), Screen.fullScreen);
			CreateOptionToggle(g, "VSYNC", "V-SYNC", (v) => sm.SetVSync(v), QualitySettings.vSyncCount > 0);
			CreateOptionSlider(g, "QUALITY", "QUALITY", (v) => sm.SetQuality(Mathf.RoundToInt(v * 2)), QualitySettings.GetQualityLevel() / 2f);
			CreateOptionToggle(g, "FPS", "SHOW FPS", (v) => sm.ToggleFPS(v), PlayerPrefs.GetInt("ShowFPS", 0) == 1);

			Transform a = m_settingsTabPanels["AUDIO"].transform;
			CreateTitleLabel(a, "AUDIO PROTOCOL", 28, new Color(0f, 0.8f, 1f));
			CreateOptionSlider(a, "VOLUME", "MASTER GAIN", (v) => sm.SetVolume(v), PlayerPrefs.GetFloat("MasterVolume", 1f));

			Transform gp = m_settingsTabPanels["GAMEPLAY"].transform;
			CreateTitleLabel(gp, "NEURAL INTERFACE", 28, new Color(0f, 0.8f, 1f));
			CreateOptionSlider(gp, "SENSITIVITY", "RESPONSE LAG", (v) => sm.SetSensitivity(Mathf.Lerp(0.1f, 3.0f, v)), (PlayerPrefs.GetFloat("MouseSensitivity", 1.0f) - 0.1f) / 2.9f);
			CreateOptionSlider(gp, "FOV", "VIEW ANGLE", (v) => sm.SetFOV(Mathf.Lerp(60f, 110f, v)), (PlayerPrefs.GetFloat("FieldOfView", 70f) - 60f) / 50f);
			CreateOptionToggle(gp, "INVERT_Y", "INVERT Y-AXIS", (v) => sm.SetInvertY(v), sm.GetInvertY());
			CreateOptionToggle(gp, "LANGUAGE", "LANGUAGE", (v) => sm.SetLanguage(v ? 1 : 0), LocalizationManager.CurrentLanguage == LocalizationManager.Language.FR, "FR", "EN");

			Transform ct = controlsContent.transform;
			CreateTitleLabel(ct, "CONTROL MAPPING", 28, new Color(0f, 0.8f, 1f));
			string[] actions = { "FORWARD", "BACKWARD", "LEFT", "RIGHT", "UP", "DOWN", "YAW_LEFT", "YAW_RIGHT", "BOOST", "BRAKE", "TRAJECTORY", "MODIFIER", "AUTOPILOT", "TARGET_ARRIVAL", "TARGET_REFUEL" };
			foreach(var act in actions) CreateKeyBindUI(ct, act);
		}

		SwitchSettingsTab("GRAPHICS");
		m_settingsPanel.SetActive(false);

		m_confirmQuitPanel = new GameObject("ConfirmQuitPanel", typeof(RectTransform));
		m_confirmQuitPanel.transform.SetParent(m_pausePanel.transform, false);
		RectTransform confRT = m_confirmQuitPanel.GetComponent<RectTransform>();
		confRT.anchorMin = new Vector2(0.5f, 0.5f);
		confRT.anchorMax = new Vector2(0.5f, 0.5f);
		confRT.pivot = new Vector2(0.5f, 0.5f);
		confRT.anchoredPosition = new Vector2(100, 0);
		confRT.sizeDelta = new Vector2(500, 480);

		m_confirmQuitPanel.AddComponent<CanvasGroup>();

		Image confBg = m_confirmQuitPanel.AddComponent<Image>();
		confBg.color = new Color(0, 0, 0, 0.98f);
		
		if (scifiShader != null) confBg.material = new Material(scifiShader);

		CreateOutlineGlow(m_confirmQuitPanel.transform, new Color(1f, 0.4f, 0.4f, 0.6f));

		VerticalLayoutGroup confVlg = m_confirmQuitPanel.AddComponent<VerticalLayoutGroup>();
		confVlg.childAlignment = TextAnchor.MiddleCenter;
		confVlg.spacing = 30f;
		confVlg.padding = new RectOffset(40, 40, 50, 50);
		confVlg.childControlWidth = true;
		confVlg.childControlHeight = false;
		confVlg.childForceExpandWidth = true;
		confVlg.childForceExpandHeight = false;

		CreateTitleLabel(m_confirmQuitPanel.transform, "ABANDON ?", 42, new Color(1f, 0.3f, 0.3f));
		CreateSubtitleLabel(m_confirmQuitPanel.transform, "> CONFIRM HANGAR RETURN", new Color(1f, 0.7f, 0.7f, 0.7f));
		CreateMenuButton(m_confirmQuitPanel.transform, "OUI",    "CONFIRM",   ConfirmQuit, new Color(0.3f, 0.05f, 0.05f, 0.6f), new Color(1f, 0.2f, 0.2f, 0.8f));
		CreateMenuButton(m_confirmQuitPanel.transform, "ANNULER","CANCEL",    CancelQuit,  new Color(0f, 0.8f, 1f, 0.05f), new Color(0f, 0.8f, 1f, 0.4f));

		m_confirmQuitPanel.SetActive(false);
		m_pausePanel.SetActive(false);
	}

	private GameObject CreatePanel(Transform parent, string name, Color color)
	{
		GameObject g = new GameObject(name);
		g.transform.SetParent(parent, false);
		Image img = g.AddComponent<Image>();
		img.color = color;
		StretchFull(img.rectTransform);
		return g;
	}

	private void StretchFull(RectTransform rt)
	{
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
	}

	private void CreateSideBar(Transform parent)
	{
		GameObject bar = new GameObject("SideBar", typeof(RectTransform));
		bar.transform.SetParent(parent, false);
		Image img = bar.AddComponent<Image>();
		img.color = new Color(0, 0, 0, 0.98f);
		RectTransform rt = img.rectTransform;
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = new Vector2(0, 1);
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = new Vector2(350, 0);

		GameObject line = new GameObject("SideBarLine");
		line.transform.SetParent(bar.transform, false);
		Image lImg = line.AddComponent<Image>();
		lImg.color = new Color(0f, 0.8f, 1f, 0.4f);
		RectTransform lRt = lImg.rectTransform;
		lRt.anchorMin = new Vector2(1, 0);
		lRt.anchorMax = Vector2.one;
		lRt.offsetMin = new Vector2(-2, 0);
		lRt.offsetMax = Vector2.zero;

		CreateTitleLabel(bar.transform, "STARS", 26, new Color(0f, 0.85f, 1f, 0.8f), new Vector2(260, 50), new Vector2(0.5f, 0.85f));
		CreateSubtitleLabel_Positioned(bar.transform, "NAVIGATION SYSTEM", new Color(0.5f, 0.7f, 1f, 0.5f), new Vector2(0.5f, 0.79f));
	}

	private void CreateOutlineGlow(Transform parent, Color color)
	{
		GameObject g = new GameObject("OutlineGlow");
		g.transform.SetParent(parent, false);
		Image img = g.AddComponent<Image>();
		img.color = Color.clear;
		Outline o = g.AddComponent<Outline>();
		o.effectColor = color;
		o.effectDistance = new Vector2(1, -1);
		RectTransform rt = img.rectTransform;
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = new Vector2(-1, -1);
		rt.offsetMax = new Vector2(1, 1);
		
		LayoutElement le = g.AddComponent<LayoutElement>();
		le.ignoreLayout = true;
	}

	private void CreateSeparatorLine(Transform parent, Color color)
	{
		GameObject g = new GameObject("Separator");
		g.transform.SetParent(parent, false);
		Image img = g.AddComponent<Image>();
		img.color = color;
		RectTransform rt = img.rectTransform;
		rt.sizeDelta = new Vector2(360, 1);
	}

	private Text CreateTitleLabel(Transform parent, string txt, int fontSize, Color color,
		Vector2 sizeDelta = default, Vector2 anchorPos = default, bool absolute = false)
	{
		GameObject g = new GameObject("Title_" + txt);
		g.transform.SetParent(parent, false);
		Text t = g.AddComponent<Text>();
		t.text = txt;
		t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		t.fontSize = fontSize;
		t.fontStyle = FontStyle.Bold;
		t.color = color;
		t.alignment = TextAnchor.MiddleCenter;
		
		var loc = g.AddComponent<LocalizedText>();
		loc.m_localizationKey = txt;

		RectTransform rt = g.GetComponent<RectTransform>();
		rt.sizeDelta = sizeDelta == default ? new Vector2(400, 80) : sizeDelta;
		if (absolute && anchorPos != default)
		{
			rt.anchorMin = anchorPos;
			rt.anchorMax = anchorPos;
			rt.anchoredPosition = Vector2.zero;
		}
		return t;
	}

	private void CreateSubtitleLabel(Transform parent, string txt, Color color)
	{
		GameObject g = new GameObject("Subtitle");
		g.transform.SetParent(parent, false);
		Text t = g.AddComponent<Text>();
		t.text = txt;
		t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		t.fontSize = 14;
		t.color = color;
		t.alignment = TextAnchor.MiddleCenter;

		var loc = g.AddComponent<LocalizedText>();
		loc.m_localizationKey = txt;

		g.GetComponent<RectTransform>().sizeDelta = new Vector2(380, 28);
	}

	private void CreateSubtitleLabel_Positioned(Transform parent, string txt, Color color, Vector2 anchor)
	{
		GameObject g = new GameObject("Subtitle_Pos", typeof(RectTransform));
		g.transform.SetParent(parent, false);
		Text t = g.AddComponent<Text>();
		t.text = txt;
		t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		t.fontSize = 12;
		t.color = color;
		t.alignment = TextAnchor.MiddleCenter;
		
		var loc = g.AddComponent<LocalizedText>();
		loc.m_localizationKey = txt;

		Outline o = g.AddComponent<Outline>();
		o.effectColor = new Color(0, 0, 0, 0.5f);
		o.effectDistance = new Vector2(1, -1);

		RectTransform rt = g.GetComponent<RectTransform>();
		rt.anchorMin = anchor;
		rt.anchorMax = anchor;
		rt.anchoredPosition = Vector2.zero;
		rt.sizeDelta = new Vector2(260, 24);
	}

	private void CreateMenuButton(Transform parent, string id, string label,
		UnityEngine.Events.UnityAction action, Color normalColor, Color glowColor)
	{
		GameObject g = new GameObject("Btn_" + id);
		g.transform.SetParent(parent, false);

		Image img = g.AddComponent<Image>();
		img.color = normalColor;
		RectTransform rt = g.GetComponent<RectTransform>();
		rt.sizeDelta = new Vector2(380, 55);

		LayoutElement le = g.AddComponent<LayoutElement>();
		le.minHeight = 55;
		le.preferredHeight = 55;

		GameObject glow = new GameObject("Glow", typeof(RectTransform), typeof(Image));
		glow.transform.SetParent(g.transform, false);
		Image glowImg = glow.GetComponent<Image>();
		glowImg.color = glowColor;
		StretchFull(glow.GetComponent<RectTransform>());
		glow.AddComponent<Outline>().effectColor = glowColor;
		glowImg.rectTransform.offsetMin = new Vector2(-1, -1);
		glowImg.rectTransform.offsetMax = new Vector2(1, 1);

		Button btn = g.AddComponent<Button>();
		btn.onClick.AddListener(action);

		UIButtonEffects fx = g.AddComponent<UIButtonEffects>();
		fx.normalColor = normalColor;
		fx.hoverColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0.4f);
		fx.pressedColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0.8f);

		GameObject tObj = new GameObject("Label");
		tObj.transform.SetParent(g.transform, false);
		Text t = tObj.AddComponent<Text>();
		t.text = label;
		t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		t.fontSize = 18;
		t.fontStyle = FontStyle.Bold;
		t.color = Color.white;
		t.alignment = TextAnchor.MiddleCenter;

		var loc = tObj.AddComponent<LocalizedText>();
		loc.m_localizationKey = label;

		RectTransform tRt = tObj.GetComponent<RectTransform>();
		StretchFull(tRt);
	}

	private void CreateOptionSlider(Transform parent, string name, string labelKey, System.Action<float> action, float initialValue)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
		go.transform.SetParent(parent, false);
		
		LayoutElement le = go.GetComponent<LayoutElement>();
		le.minHeight = 45; le.preferredHeight = 45;

		RectTransform rt = go.GetComponent<RectTransform>();
		rt.sizeDelta = new Vector2(400, 45);

		Text t = CreateTextWithLocalized(go.transform, labelKey, 14, new Color(0.7f, 0.9f, 1f), TextAnchor.MiddleLeft);
		RectTransform lrt = t.GetComponent<RectTransform>();
		lrt.anchorMin = new Vector2(0.05f, 0); lrt.anchorMax = new Vector2(0.45f, 1);
		lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
		
		GameObject sliderObj = new GameObject("Slider", typeof(RectTransform));
		sliderObj.transform.SetParent(go.transform, false);
		RectTransform srt = sliderObj.GetComponent<RectTransform>();
		srt.anchorMin = new Vector2(0.55f, 0.5f); srt.anchorMax = new Vector2(0.95f, 0.5f);
		srt.sizeDelta = new Vector2(0, 10);
		srt.anchoredPosition = Vector2.zero;

		Slider slider = sliderObj.AddComponent<Slider>();
		
		GameObject back = new GameObject("Background", typeof(RectTransform), typeof(Image));
		back.transform.SetParent(sliderObj.transform, false);
		back.GetComponent<Image>().color = new Color(0, 0.2f, 0.4f, 0.3f);
		StretchFull(back.GetComponent<RectTransform>());

		GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
		fillArea.transform.SetParent(sliderObj.transform, false);
		RectTransform fart = fillArea.GetComponent<RectTransform>();
		fart.anchorMin = Vector2.zero; fart.anchorMax = Vector2.one;
		fart.offsetMin = new Vector2(5, 0); fart.offsetMax = new Vector2(-5, 0);

		GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
		fill.transform.SetParent(fillArea.transform, false);
		fill.GetComponent<Image>().color = new Color(0, 0.8f, 1f, 0.8f);
		StretchFull(fill.GetComponent<RectTransform>());
		
		GameObject handleArea = new GameObject("Handle Area", typeof(RectTransform));
		handleArea.transform.SetParent(sliderObj.transform, false);
		StretchFull(handleArea.GetComponent<RectTransform>());
		
		GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
		handle.transform.SetParent(handleArea.transform, false);
		handle.GetComponent<Image>().color = Color.white;
		RectTransform hrt = handle.GetComponent<RectTransform>();
		hrt.anchorMin = new Vector2(0, 0); hrt.anchorMax = new Vector2(0, 1);
		hrt.sizeDelta = new Vector2(10, 0);

		slider.fillRect = fill.GetComponent<RectTransform>();
		slider.handleRect = hrt;
		slider.targetGraphic = handle.GetComponent<Image>();
		slider.value = initialValue;
		slider.onValueChanged.AddListener((v) => action(v));
	}

	private void CreateOptionToggle(Transform parent, string name, string labelKey, System.Action<bool> action, bool initialValue, string onLabelOverride = null, string offLabelOverride = null)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
		go.transform.SetParent(parent, false);
		
		LayoutElement le = go.GetComponent<LayoutElement>();
		le.minHeight = 45; le.preferredHeight = 45;

		Text t = CreateTextWithLocalized(go.transform, labelKey, 14, new Color(0.7f, 0.9f, 1f), TextAnchor.MiddleLeft);
		RectTransform lrt = t.GetComponent<RectTransform>();
		lrt.anchorMin = new Vector2(0.05f, 0); lrt.anchorMax = new Vector2(0.45f, 1);
		lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
		
		GameObject btnObj = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Button));
		btnObj.transform.SetParent(go.transform, false);
		RectTransform brt = btnObj.GetComponent<RectTransform>();
		brt.anchorMin = new Vector2(0.55f, 0.5f); brt.anchorMax = new Vector2(0.95f, 0.5f);
		brt.sizeDelta = new Vector2(0, 30);
		brt.anchoredPosition = Vector2.zero;

		Image img = btnObj.GetComponent<Image>();
		img.color = initialValue ? new Color(0, 0.8f, 1f, 0.4f) : new Color(0, 0.1f, 0.2f, 0.6f);
		
		Button b = btnObj.GetComponent<Button>();
		GameObject valObj = new GameObject("Value", typeof(RectTransform));
		valObj.transform.SetParent(btnObj.transform, false);
		Text vt = valObj.AddComponent<Text>();
		
		bool state = initialValue;
		if (onLabelOverride == null && offLabelOverride == null)
		{
			var loc = valObj.AddComponent<LocalizedText>();
			loc.m_localizationKey = state ? "ON" : "OFF";
			loc.Refresh();
		}
		else vt.text = state ? onLabelOverride : offLabelOverride;

		vt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); vt.fontSize = 14; vt.color = Color.white;
		vt.alignment = TextAnchor.MiddleCenter;
		StretchFull(valObj.GetComponent<RectTransform>());

		b.onClick.AddListener(() => {
			state = !state;
			img.color = state ? new Color(0, 0.8f, 1f, 0.4f) : new Color(0, 0.1f, 0.2f, 0.6f);
			if (onLabelOverride == null && offLabelOverride == null)
			{
				var loc = valObj.GetComponent<LocalizedText>();
				if (loc) { loc.m_localizationKey = state ? "ON" : "OFF"; loc.Refresh(); }
			}
			else vt.text = state ? onLabelOverride : offLabelOverride;
			action(state);
		});
	}

	private Text CreateTextWithLocalized(Transform p, string textKey, int fontSize, Color color, TextAnchor alignment, bool localized = true)
	{
		GameObject g = new GameObject("Text");
		g.transform.SetParent(p, false);
		Text t = g.AddComponent<Text>();
		t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		t.fontSize = fontSize;
		t.color = color;
		t.alignment = alignment;
		t.horizontalOverflow = HorizontalWrapMode.Overflow;
		t.verticalOverflow = VerticalWrapMode.Overflow;
		t.text = textKey;
		if (localized)
		{
			var loc = g.AddComponent<LocalizedText>();
			loc.m_localizationKey = textKey;
		}
		return t;
	}
	private void SwitchSettingsTab(string tabName)
	{
		m_currentTab = tabName;
		foreach (var kvp in m_settingsTabPanels)
			kvp.Value.SetActive(kvp.Key == tabName);
	}

	private GameObject CreateSettingsTabPanel(Transform parent, string name, bool scrollable = false)
	{
		if (scrollable)
		{
			GameObject scrollObj = new GameObject("ScrollContainer", typeof(RectTransform), typeof(ScrollRect));
			scrollObj.transform.SetParent(parent, false);
			StretchFull(scrollObj.GetComponent<RectTransform>());
			
			GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
			viewportObj.transform.SetParent(scrollObj.transform, false);
			StretchFull(viewportObj.GetComponent<RectTransform>());

			GameObject contentObj = new GameObject(name, typeof(RectTransform));
			contentObj.transform.SetParent(viewportObj.transform, false);
			RectTransform crt = contentObj.GetComponent<RectTransform>();
			crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1);
			crt.pivot = new Vector2(0.5f, 1);
			crt.anchoredPosition = Vector2.zero;
			crt.sizeDelta = new Vector2(0, 500);

			VerticalLayoutGroup vlg = contentObj.AddComponent<VerticalLayoutGroup>();
			vlg.padding = new RectOffset(20, 20, 20, 20);
			vlg.spacing = 10;
			vlg.childControlWidth = true; vlg.childControlHeight = true;
			vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
			
			contentObj.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			
			ScrollRect sr = scrollObj.GetComponent<ScrollRect>();
			sr.viewport = viewportObj.GetComponent<RectTransform>();
			sr.content = crt;
			sr.horizontal = false; sr.vertical = true;
			sr.scrollSensitivity = 30f;
			sr.movementType = ScrollRect.MovementType.Clamped;

			return contentObj;
		}
		else
		{
			GameObject g = new GameObject(name, typeof(RectTransform));
			g.transform.SetParent(parent, false);
			StretchFull(g.GetComponent<RectTransform>());
			VerticalLayoutGroup vlg = g.AddComponent<VerticalLayoutGroup>();
			vlg.padding = new RectOffset(10, 10, 20, 20);
			vlg.spacing = 20;
			vlg.childAlignment = TextAnchor.UpperCenter;
			vlg.childControlWidth = true; vlg.childControlHeight = false;
			vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
			return g;
		}
	}

	private void CreateTabButton(Transform parent, string id, string labelKey, UnityEngine.Events.UnityAction action)
	{
		GameObject g = new GameObject("Tab_" + id, typeof(RectTransform), typeof(Image), typeof(Button));
		g.transform.SetParent(parent, false);
		LayoutElement le = g.AddComponent<LayoutElement>();
		le.minHeight = 45; le.preferredHeight = 45;
		
		Image img = g.GetComponent<Image>();
		img.color = new Color(0, 0.8f, 1f, 0.05f);
		
		Button btn = g.GetComponent<Button>();
		btn.onClick.AddListener(action);
		
		UIButtonEffects fx = g.AddComponent<UIButtonEffects>();
		fx.normalColor = img.color;
		fx.hoverColor = new Color(0, 0.8f, 1f, 0.25f);
		fx.pressedColor = new Color(0, 0.8f, 1f, 0.5f);

		Text t = CreateTextWithLocalized(g.transform, labelKey, 14, Color.white, TextAnchor.MiddleCenter);
		StretchFull(t.GetComponent<RectTransform>());
	}

	private void CreateKeyBindUI(Transform parent, string actionName)
	{
		GameObject go = new GameObject("Bind_" + actionName, typeof(RectTransform), typeof(LayoutElement));
		go.transform.SetParent(parent, false);
		LayoutElement le = go.GetComponent<LayoutElement>();
		le.minHeight = 35; le.preferredHeight = 35;

		Text t = CreateTextWithLocalized(go.transform, actionName, 12, new Color(0.8f, 0.9f, 1f), TextAnchor.MiddleLeft);
		RectTransform trt = t.GetComponent<RectTransform>();
		trt.anchorMin = new Vector2(0.05f, 0); trt.anchorMax = new Vector2(0.45f, 1);
		trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
		
		GameObject btnObj = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
		btnObj.transform.SetParent(go.transform, false);
		Image img = btnObj.GetComponent<Image>();
		img.color = new Color(0, 0.8f, 1f, 0.15f);
		
		RectTransform brt = btnObj.GetComponent<RectTransform>();
		brt.anchorMin = new Vector2(0.55f, 0.5f); brt.anchorMax = new Vector2(0.85f, 0.5f);
		brt.sizeDelta = new Vector2(0, 28);
		brt.anchoredPosition = Vector2.zero;

		Text kt = CreateTextWithLocalized(btnObj.transform, (InputRemapper.Binds != null && InputRemapper.Binds.ContainsKey(actionName)) ? InputRemapper.Binds[actionName].ToString() : "---", 13, Color.cyan, TextAnchor.MiddleCenter, false);
		StretchFull(kt.GetComponent<RectTransform>());

		Button b = btnObj.GetComponent<Button>();
		b.onClick.AddListener(() => {
			kt.text = "...";
			StartCoroutine(WaitForKey(actionName, kt));
		});

		GameObject resObj = new GameObject("Reset", typeof(RectTransform), typeof(Image), typeof(Button));
		resObj.transform.SetParent(go.transform, false);
		RectTransform rrt = resObj.GetComponent<RectTransform>();
		rrt.anchorMin = new Vector2(0.88f, 0.5f); rrt.anchorMax = new Vector2(0.97f, 0.5f);
		rrt.sizeDelta = new Vector2(0, 28);
		rrt.anchoredPosition = Vector2.zero;
		
		Image rImg = resObj.GetComponent<Image>();
		rImg.color = new Color(1f, 0.3f, 0.3f, 0.2f);
		
		Text resTxt = CreateTextWithLocalized(resObj.transform, "⟳", 18, Color.white, TextAnchor.MiddleCenter);
		StretchFull(resTxt.GetComponent<RectTransform>());

		Button rb = resObj.GetComponent<Button>();
		rb.onClick.AddListener(() => {
			InputRemapper.ResetBind(actionName);
			kt.text = InputRemapper.Binds[actionName].ToString();
		});
	}

	private IEnumerator WaitForKey(string actionName, Text display)
	{
		yield return new WaitForSecondsRealtime(0.1f);
		bool pressed = false;
		while (!pressed)
		{
			var kb = Keyboard.current;
			if (kb != null && kb.anyKey.wasPressedThisFrame)
			{
				foreach (var keyControl in kb.allKeys)
				{
					if (keyControl.wasPressedThisFrame)
					{
						Key k = keyControl.keyCode;
						if (k == Key.Escape) { pressed = true; break; }
						InputRemapper.SetBind(actionName, k);
						display.text = k.ToString();
						pressed = true; break;
					}
				}
			}
			yield return null;
		}
	}
}
