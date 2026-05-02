using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class GameMenuManager : MonoBehaviour
{
	[Header("Panels")]
	public CanvasGroup m_menuCanvasGroup;
	public GameObject m_mainMenuPanel;
	public GameObject m_optionsMenuPanel;

	[Header("Baked UI Panels")]
	public GameObject m_graphicsPanel;
	public GameObject m_audioPanel;
	public GameObject m_gameplayPanel;

	[Header("Transition Settings")]
	public Transform m_menuCameraPoint;
	public float m_transitionDuration = 2.5f;
	public AnimationCurve m_transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

	[Header("Game References")]
	public ShipControl m_playerShip;

	private Transform m_mainCameraTransform;
	private Vector3 m_gameLocalPos;
	private Quaternion m_gameLocalRot;
	private Transform m_originalCameraParent;

	private void Reset()
	{
		m_menuCanvasGroup = GetComponent<CanvasGroup>();
		if (m_menuCanvasGroup == null) m_menuCanvasGroup = gameObject.AddComponent<CanvasGroup>();
		if (m_playerShip == null) m_playerShip = FindAnyObjectByType<ShipControl>();
		if (m_mainMenuPanel == null) m_mainMenuPanel = transform.Find("MainMenu_Panel")?.gameObject;
		if (m_optionsMenuPanel == null) m_optionsMenuPanel = transform.Find("OptionsMenu_Panel")?.gameObject;
	}

	private void Start()
	{
		if (m_playerShip == null) m_playerShip = FindAnyObjectByType<ShipControl>();
		if (Camera.main != null)
		{
			m_mainCameraTransform = Camera.main.transform;
			m_originalCameraParent = m_mainCameraTransform.parent;
			m_gameLocalPos = m_mainCameraTransform.localPosition;
			m_gameLocalRot = m_mainCameraTransform.localRotation;
			if (m_menuCameraPoint == null && m_playerShip != null)
			{
				GameObject tempPoint = new GameObject("Auto_MenuCameraPoint");
				tempPoint.transform.position = m_playerShip.transform.position + m_playerShip.transform.forward * 10f + m_playerShip.transform.right * 5f;
				tempPoint.transform.LookAt(m_playerShip.transform);
				m_menuCameraPoint = tempPoint.transform;
			}
			if (m_menuCameraPoint != null)
			{
				m_mainCameraTransform.parent = null; 
				m_mainCameraTransform.position = m_menuCameraPoint.position;
				m_mainCameraTransform.rotation = m_menuCameraPoint.rotation;
			}
		}
		SetupRuntimeListeners();
		if (m_mainMenuPanel != null) m_mainMenuPanel.SetActive(true);
		if (m_optionsMenuPanel != null) m_optionsMenuPanel.SetActive(false);
		if (m_menuCanvasGroup != null) m_menuCanvasGroup.alpha = 1f;
		UpdateTabVisibility("GRAPHICS");
		if (m_playerShip != null) m_playerShip.enabled = false;
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
	}

	private void SetupRuntimeListeners()
	{
		if (m_mainMenuPanel != null) {
			ReconnectButton(m_mainMenuPanel.transform, "Btn_LAUNCH MISSION", PlayGame);
			ReconnectButton(m_mainMenuPanel.transform, "Btn_SYSTEM SETTINGS", ShowOptions);
			ReconnectButton(m_mainMenuPanel.transform, "Btn_QUIT", QuitGame);
		}
		if (m_optionsMenuPanel != null) {
			Transform tabs = m_optionsMenuPanel.transform.Find("OptionsRoot/Sidebar/TabsContainer");
			if (tabs == null) tabs = m_optionsMenuPanel.transform;
			ReconnectButton(tabs, "Tab_GRAPHICS", () => SelectTabAtRuntime("GRAPHICS"));
			ReconnectButton(tabs, "Tab_AUDIO", () => SelectTabAtRuntime("AUDIO"));
			ReconnectButton(tabs, "Tab_GAMEPLAY", () => SelectTabAtRuntime("GAMEPLAY"));
			ReconnectButton(m_optionsMenuPanel.transform, "Btn_Back", ShowMainMenu);
			SettingsManager sm = FindAnyObjectByType<SettingsManager>();
			if (sm != null) {
				if (m_graphicsPanel != null) {
					Transform resSel = m_graphicsPanel.transform.Find("RESOLUTION_Dropdown/Selector");
					if (resSel) {
						Button lAr = resSel.Find("Arrow_L")?.GetComponent<Button>();
						Button rAr = resSel.Find("Arrow_R")?.GetComponent<Button>();
						Text valT = resSel.Find("Value")?.GetComponent<Text>();
						var options = GetResolutionOptions();
						int resIdx = GetCurrentResolutionIndex();
						if (lAr) { lAr.onClick.RemoveAllListeners(); lAr.onClick.AddListener(() => { resIdx = (resIdx - 1 + options.Count) % options.Count; if(valT) valT.text = options[resIdx]; sm.SetResolution(resIdx); }); }
						if (rAr) { rAr.onClick.RemoveAllListeners(); rAr.onClick.AddListener(() => { resIdx = (resIdx + 1) % options.Count; if(valT) valT.text = options[resIdx]; sm.SetResolution(resIdx); }); }
					}
					SetupToggle(m_graphicsPanel.transform, "FULLSCREEN_Toggle", (v) => sm.SetFullscreen(v));
					SetupToggle(m_graphicsPanel.transform, "V-SYNC_Toggle", (v) => sm.SetVSync(v));
					Transform qBtns = m_graphicsPanel.transform.Find("Quality_Group/Buttons");
					if (qBtns) {
						string[] qNames = { "LOW", "MED", "ULTRA" };
						for (int i = 0; i < 3; i++) {
							int idx = i;
							Button b = qBtns.Find("Q_" + qNames[i])?.GetComponent<Button>();
							if (b) {
								b.onClick.RemoveAllListeners();
								b.onClick.AddListener(() => {
									sm.SetQuality(idx);
									for(int j=0; j<3; j++) {
										Image img = qBtns.Find("Q_"+qNames[j])?.GetComponent<Image>();
										if (img) img.color = (j==idx) ? new Color(0, 0.8f, 1f, 0.6f) : new Color(0, 0.1f, 0.2f, 0.6f);
										UIButtonEffects fxQ = qBtns.Find("Q_"+qNames[j])?.GetComponent<UIButtonEffects>();
										if (fxQ) fxQ.normalColor = img.color;
									}
								});
							}
						}
					}
				}
				if (m_audioPanel != null) SetupSlider(m_audioPanel.transform, "MASTER GAIN_Group", (v) => sm.SetVolume(v));
				if (m_gameplayPanel != null) {
					SetupSlider(m_gameplayPanel.transform, "RESPONSE LAG_Group", (v) => sm.SetSensitivity(v));
					SetupSlider(m_gameplayPanel.transform, "VIEW ANGLE_Group", (v) => sm.SetFOV(v));
				}
			}
		}
	}

	private void ReconnectButton(Transform root, string name, UnityEngine.Events.UnityAction action) {
		Transform t = root.Find(name);
		if (t == null) t = FindChildRecursive(root, name);
		if (t != null) {
			Button b = t.GetComponent<Button>();
			if (b) { b.onClick.RemoveAllListeners(); b.onClick.AddListener(action); }
		}
	}

	private void SetupToggle(Transform root, string name, System.Action<bool> action) {
		Transform t = root.Find(name);
		if (t == null) t = FindChildRecursive(root, name);
		if (t == null) return;
		Button b = t.GetComponentInChildren<Button>();
		Text valT = b?.transform.Find("Value")?.GetComponent<Text>();
		Image bg = b?.GetComponent<Image>();
		if (b && valT && bg) {
			b.onClick.RemoveAllListeners();
			b.onClick.AddListener(() => {
				bool current = (valT.text == "OFF");
				valT.text = current ? "ON" : "OFF";
				valT.color = current ? new Color(0, 0.8f, 1f, 1f) : new Color(0.4f, 0.4f, 0.4f, 1f);
				bg.color = current ? new Color(0, 0.2f, 0.4f, 0.6f) : new Color(0, 0.1f, 0.2f, 0.4f);
				UIButtonEffects fx = b.GetComponent<UIButtonEffects>();
				if (fx) fx.normalColor = bg.color;
				action(current);
			});
		}
	}

	private void SetupSlider(Transform root, string name, System.Action<float> action) {
		Transform t = root.Find(name);
		if (t == null) t = FindChildRecursive(root, name);
		if (t != null) {
			Slider s = t.GetComponentInChildren<Slider>();
			if (s) { s.onValueChanged.RemoveAllListeners(); s.onValueChanged.AddListener((v) => action(v)); }
		}
	}

	private Transform FindChildRecursive(Transform parent, string name) {
		if (parent.name == name) return parent;
		foreach (Transform child in parent) {
			Transform found = FindChildRecursive(child, name);
			if (found != null) return found;
		}
		return null;
	}

	private List<string> GetResolutionOptions() {
		List<string> options = new List<string>();
		foreach (var res in Screen.resolutions) options.Add(res.width + "x" + res.height);
		if (options.Count == 0) options.Add(Screen.width + "x" + Screen.height);
		return options;
	}

	private int GetCurrentResolutionIndex() {
		int saved = PlayerPrefs.GetInt("ResolutionIndex", -1);
		if (saved != -1) return saved;
		Resolution[] res = Screen.resolutions;
		for (int i = 0; i < res.Length; i++) if (res[i].width == Screen.width && res[i].height == Screen.height) return i;
		return 0;
	}

	private string m_currentTab = "GRAPHICS";

	public void SelectTabAtRuntime(string tabName) { m_currentTab = tabName; UpdateTabVisibility(tabName); }

	private void UpdateTabVisibility(string tabName)
	{
		if (m_graphicsPanel != null) m_graphicsPanel.SetActive(tabName == "GRAPHICS");
		if (m_audioPanel != null) m_audioPanel.SetActive(tabName == "AUDIO");
		if (m_gameplayPanel != null) m_gameplayPanel.SetActive(tabName == "GAMEPLAY");
		if (m_optionsMenuPanel != null) {
			Transform tabs = m_optionsMenuPanel.transform.Find("OptionsRoot/Sidebar/TabsContainer");
			if (tabs != null) {
				foreach (Transform t in tabs) {
					if (!t.name.StartsWith("Tab_")) continue;
					bool isActive = (t.name == "Tab_" + tabName);
					Image img = t.GetComponent<Image>();
					Color targetCol = isActive ? new Color(0, 0.8f, 1f, 0.15f) : new Color(1, 1, 1, 0.02f);
					if (img) img.color = targetCol;
					UIButtonEffects fx = t.GetComponent<UIButtonEffects>();
					if (fx != null) {
						fx.normalColor = targetCol;
						Color.RGBToHSV(targetCol, out float h, out float s, out float v);
						fx.hoverColor = Color.HSVToRGB(h, s, Mathf.Clamp01(v + 0.3f));
						fx.hoverColor.a = targetCol.a * 2f;
						typeof(UIButtonEffects).GetField("targetColor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(fx, targetCol);
					}
					Transform indicator = t.Find("Indicator");
					if (indicator) indicator.gameObject.SetActive(isActive);
					Text txt = t.Find("Text")?.GetComponent<Text>();
					if (txt) txt.color = isActive ? Color.white : new Color(1, 1, 1, 0.3f);
				}
			}
		}
	}

	public void ShowMainMenu() { StartCoroutine(TransitionPanels(m_optionsMenuPanel, m_mainMenuPanel)); }
	public void ShowOptions() { StartCoroutine(TransitionPanels(m_mainMenuPanel, m_optionsMenuPanel)); }

	private IEnumerator TransitionPanels(GameObject from, GameObject to)
	{
		float duration = 0.4f; float elapsed = 0;
		CanvasGroup fromGroup = from?.GetComponent<CanvasGroup>();
		CanvasGroup toGroup = to?.GetComponent<CanvasGroup>();
		if (to != null) to.SetActive(true);
		while (elapsed < duration) {
			elapsed += Time.deltaTime; float t = elapsed / duration; float ease = Mathf.SmoothStep(0, 1, t);
			if (fromGroup != null) { fromGroup.alpha = 1 - ease; from.transform.localScale = Vector3.one * (1 - ease * 0.1f); }
			if (toGroup != null) { toGroup.alpha = ease; to.transform.localScale = Vector3.one * (0.9f + ease * 0.1f); }
			yield return null;
		}
		if (from != null) from.SetActive(false);
	}

	public void PlayGame() {
		Transform shipTransform = GameObject.Find("Menu_Ship")?.transform ?? m_playerShip?.transform;
		if (shipTransform != null) ShipLaunchCinematic.StartLaunch(this, shipTransform, m_menuCanvasGroup, m_mainCameraTransform);
		else SceneManager.LoadSceneAsync("Galaxie");
	}

	public void QuitGame()
	{
		Application.Quit();
#if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
#endif
	}
}
