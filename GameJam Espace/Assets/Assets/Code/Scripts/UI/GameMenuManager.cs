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
	public GameObject m_controlsPanel;

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
		if (m_optionsMenuPanel != null) {
			if (m_graphicsPanel == null) m_graphicsPanel = FindPanel(m_optionsMenuPanel.transform, "Graphics");
			if (m_audioPanel == null) m_audioPanel = FindPanel(m_optionsMenuPanel.transform, "Audio");
			if (m_gameplayPanel == null) m_gameplayPanel = FindPanel(m_optionsMenuPanel.transform, "Gameplay");
			if (m_controlsPanel == null) m_controlsPanel = FindPanel(m_optionsMenuPanel.transform, "Controls");
		}
	}

	private GameObject EnsurePanelExists(Transform root, GameObject current, string name, string titleOverride) {
		if (current != null) return current;
		Transform existing = root.Find(name);
		if (existing != null) return existing.gameObject;

		if (m_graphicsPanel != null) {
			GameObject clone = Instantiate(m_graphicsPanel, root);
			clone.name = name;
			clone.SetActive(false);
			RectTransform crt = clone.GetComponent<RectTransform>();
			if (crt != null) {
				crt.localPosition = Vector3.zero;
				crt.localScale = Vector3.one;
				crt.anchoredPosition = Vector2.zero;
			}

			foreach(Transform childInClone in clone.transform) {
				string cn = childInClone.name.ToUpper();
				if (cn.Contains("TITLE") || cn.Contains("HEADER") || cn.Contains("BACKGROUND")) continue;
				childInClone.gameObject.SetActive(false);
			}

			Transform title = FindChildRecursive(clone.transform, "Title");
			if (title == null) title = FindChildRecursive(clone.transform, "Header");
			if (title != null) {
				Text t = title.GetComponentInChildren<Text>();
				if (t != null) {
					LocalizedText loc = t.GetComponent<LocalizedText>();
					if (loc == null) loc = t.gameObject.AddComponent<LocalizedText>();
					loc.m_localizationKey = titleOverride;
					loc.Refresh();
				}
			}
			
			return clone;
		}
		return null;
	}

	private void DumpHierarchy(Transform t, string indent) {
		
		foreach (Transform child in t) DumpHierarchy(child, indent + "  ");
	}

	private GameObject FindPanel(Transform root, string baseName) {
		string[] variations = { 
			baseName + "_Panel", baseName + "Panel", baseName, 
			baseName + "_Group", baseName + "Group",
			baseName + "Content", baseName + "_Content",
			baseName.ToUpper() + "_Panel", baseName.ToUpper(),
			baseName.ToUpper() + "_Group", baseName.ToUpper() + "Content"
		};
		foreach(var v in variations) {
			Transform t = FindChildRecursive(root, v);
			if (t != null) {
				string path = GetGameObjectPath(t.gameObject);
				
				return t.gameObject;
			}
		}
		return null;
	}

	private void RefreshAllLocalizations() {
		LocalizedText[] allLoc = GetComponentsInChildren<LocalizedText>(true);
		foreach(var loc in allLoc)
			loc.Refresh();
	}

	private string GetGameObjectPath(GameObject obj) {
		string path = "/" + obj.name;
		Transform current = obj.transform;
		while (current.parent != null) {
			current = current.parent;
			path = "/" + current.name + path;
		}
		return path;
	}

	private IEnumerator Start()
	{
		InputRemapper.Load();

		if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
		{
			GameObject es = new GameObject("EventSystem");
			es.AddComponent<UnityEngine.EventSystems.EventSystem>();
			es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
			Debug.Log("[GameMenuManager] EventSystem auto-créé pour le build.");
		}
		
		if (m_optionsMenuPanel != null) {
			Transform root = m_optionsMenuPanel.transform.Find("OptionsRoot");
			if (root != null) {
				Transform sidebar = root.Find("Sidebar");
				
				if (m_graphicsPanel == null) {
					foreach (Transform child in root) {
						if (child != sidebar && child.name.Contains("Content") || child.name.Contains("Panel") || child.name.ToUpper().Contains("GRAPHIC")) {
							m_graphicsPanel = child.gameObject;
							
							break;
						}
					}
					if (m_graphicsPanel == null) {
						foreach (Transform child in root) {
							if (child != sidebar) { m_graphicsPanel = child.gameObject; break; }
						}
					}
				}

				m_audioPanel = EnsurePanelExists(root, m_audioPanel, "Audio_Panel", "AUDIO PROTOCOL");
				m_gameplayPanel = EnsurePanelExists(root, m_gameplayPanel, "Gameplay_Panel", "NEURAL INTERFACE");
				m_controlsPanel = EnsurePanelExists(root, m_controlsPanel, "Controls_Panel", "CONTROL MAPPING");
			}
		}

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

		if (m_mainMenuPanel != null) {
			GameObject verObj = new GameObject("VersionLabel", typeof(RectTransform));
			verObj.transform.SetParent(m_mainMenuPanel.transform, false);
			RectTransform vrt = verObj.GetComponent<RectTransform>();
			vrt.anchorMin = new Vector2(1, 0); vrt.anchorMax = new Vector2(1, 0);
			vrt.pivot = new Vector2(1, 0);
			vrt.anchoredPosition = new Vector2(-10, 10);
			vrt.sizeDelta = new Vector2(200, 30);
			Text vt = verObj.AddComponent<Text>();
			vt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			vt.fontSize = 12; vt.color = new Color(1, 1, 1, 0.4f);
			vt.alignment = TextAnchor.LowerRight;
			vt.text = "v" + Application.version;
		}

		// Attendre une image pour être sûr de passer après le Start du vaisseau
		yield return null;
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
	}

	private void SetupRuntimeListeners()
	{
		if (m_optionsMenuPanel != null) {
			Transform root = m_optionsMenuPanel.transform.Find("OptionsRoot");
		}
		if (m_mainMenuPanel != null) {
			ReconnectButton(m_mainMenuPanel.transform, "Btn_LAUNCH MISSION", PlayGame);
			ReconnectButton(m_mainMenuPanel.transform, "Btn_SYSTEM SETTINGS", ShowOptions);
			ReconnectButton(m_mainMenuPanel.transform, "Btn_QUIT", QuitGame);

			Transform mainTitle = m_mainMenuPanel.transform.Find("Title");
			if (mainTitle == null) mainTitle = m_mainMenuPanel.transform.Find("Header");
			if (mainTitle != null) {
				Text mt = mainTitle.GetComponentInChildren<Text>();
				if (mt != null) {
					LocalizedText mloc = mt.GetComponent<LocalizedText>();
					if (mloc == null) mloc = mt.gameObject.AddComponent<LocalizedText>();
					mloc.m_localizationKey = "STARS";
					mloc.Refresh();
				}
			}
			
			string[] keys = { "LAUNCH MISSION", "SYSTEM SETTINGS", "QUIT" };
			string[] btnNames = { "Btn_LAUNCH MISSION", "Btn_SYSTEM SETTINGS", "Btn_QUIT" };
			for(int i=0; i<keys.Length; i++) {
				Transform b = m_mainMenuPanel.transform.Find(btnNames[i]);
				if (b == null) b = FindChildRecursive(m_mainMenuPanel.transform, btnNames[i]);
				if (b != null) {
					Text t = b.GetComponentInChildren<Text>();
					if (t != null) {
						LocalizedText loc = t.GetComponent<LocalizedText>();
						if (loc == null) loc = t.gameObject.AddComponent<LocalizedText>();
						loc.m_localizationKey = keys[i];
						loc.Refresh();
					}
				}
			}
		}
		if (m_optionsMenuPanel != null) {
			Transform tabs = m_optionsMenuPanel.transform.Find("OptionsRoot/Sidebar/TabsContainer");
			if (tabs == null) tabs = m_optionsMenuPanel.transform;

			if (tabs.Find("Tab_CONTROLS") == null) {
				Transform gameplayTab = tabs.Find("Tab_GAMEPLAY");
				if (gameplayTab != null) {
					GameObject newTab = Instantiate(gameplayTab.gameObject, tabs);
					if (newTab != null) {
						newTab.name = "Tab_CONTROLS";
						Text t = newTab.GetComponentInChildren<Text>();
						if (t) t.text = "CONTROLS";
						LocalizedText loc = newTab.GetComponentInChildren<LocalizedText>();
						if (loc == null) loc = newTab.AddComponent<LocalizedText>();
						if (loc != null) loc.m_localizationKey = "CONTROLS";
						
					}
				}
			}

			ReconnectButton(tabs, "Tab_GRAPHICS", () => SelectTabAtRuntime("GRAPHICS"));
			ReconnectButton(tabs, "Tab_AUDIO", () => SelectTabAtRuntime("AUDIO"));
			ReconnectButton(tabs, "Tab_GAMEPLAY", () => SelectTabAtRuntime("GAMEPLAY"));
			ReconnectButton(tabs, "Tab_CONTROLS", () => SelectTabAtRuntime("CONTROLS"));

			string[] tabNames = { "Tab_GRAPHICS", "Tab_AUDIO", "Tab_GAMEPLAY", "Tab_CONTROLS" };
			string[] tabKeys = { "GRAPHICS", "AUDIO", "GAMEPLAY", "CONTROLS" };
			for(int i=0; i<tabNames.Length; i++) {
				Transform t = tabs.Find(tabNames[i]);
				if (t != null) {
					Text txt = t.GetComponentInChildren<Text>();
					if (txt != null) {
						LocalizedText loc = txt.GetComponent<LocalizedText>();
						if (loc == null) loc = txt.gameObject.AddComponent<LocalizedText>();
						loc.m_localizationKey = tabKeys[i];
						loc.Refresh();
					}
				}
			}

			ReconnectButton(m_optionsMenuPanel.transform, "Btn_Back", ShowMainMenu);
			Transform backBtn = FindChildRecursive(m_optionsMenuPanel.transform, "Btn_Back");
			if (backBtn != null) {
				Text bt = backBtn.GetComponentInChildren<Text>();
				if (bt != null) {
					LocalizedText bloc = bt.GetComponent<LocalizedText>();
					if (bloc == null) bloc = bt.gameObject.AddComponent<LocalizedText>();
					bloc.m_localizationKey = "BACK";
					bloc.Refresh();
				}
			}

			Transform root = m_optionsMenuPanel.transform.Find("OptionsRoot");
			Transform titleObj = null;
			if (root != null) {
				titleObj = root.Find("Title");
				if (titleObj == null) titleObj = root.Find("Header");
			}
			if (titleObj == null && tabs.parent != null) {
				titleObj = tabs.parent.Find("Title");
				if (titleObj == null) titleObj = tabs.parent.Find("Header");
			}

			if (titleObj != null) {
				Text st = titleObj.GetComponentInChildren<Text>();
				if (st != null) {
					LocalizedText sloc = st.GetComponent<LocalizedText>();
					if (sloc == null) sloc = st.gameObject.AddComponent<LocalizedText>();
					sloc.m_localizationKey = "SYSTEM SETTINGS";
					sloc.Refresh();
				}
			}

			SettingsManager sm = FindAnyObjectByType<SettingsManager>();
			if (sm != null) {
				if (m_graphicsPanel != null) {
					Transform gTitle = FindChildRecursive(m_graphicsPanel.transform, "Title");
					if (gTitle == null) gTitle = FindChildRecursive(m_graphicsPanel.transform, "Header");
					if (gTitle != null) {
						Text gt = gTitle.GetComponentInChildren<Text>();
						if (gt != null) {
							LocalizedText gloc = gt.GetComponent<LocalizedText>();
							if (gloc == null) gloc = gt.gameObject.AddComponent<LocalizedText>();
							gloc.m_localizationKey = "GRAPHICS PROTOCOL";
							gloc.Refresh();
						}
					}

					Transform resSel = m_graphicsPanel.transform.Find("RESOLUTION_Dropdown/Selector");
					if (resSel) {
						Button lAr = resSel.Find("Arrow_L")?.GetComponent<Button>();
						Button rAr = resSel.Find("Arrow_R")?.GetComponent<Button>();
						Text valT = resSel.Find("Value")?.GetComponent<Text>();
						var options = GetResolutionOptions();
						int resIdx = GetCurrentResolutionIndex();
						if (valT) valT.text = options[resIdx];
						if (lAr) { lAr.onClick.RemoveAllListeners(); lAr.onClick.AddListener(() => { 
							resIdx = (resIdx - 1 + options.Count) % options.Count; 
							if(valT) valT.text = options[resIdx]; 
							string[] parts = options[resIdx].Split('x');
							sm.SetResolution(int.Parse(parts[0]), int.Parse(parts[1])); 
						}); }
						if (rAr) { rAr.onClick.RemoveAllListeners(); rAr.onClick.AddListener(() => { 
							resIdx = (resIdx + 1) % options.Count; 
							if(valT) valT.text = options[resIdx]; 
							string[] parts = options[resIdx].Split('x');
							sm.SetResolution(int.Parse(parts[0]), int.Parse(parts[1])); 
						}); }
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
								Text bt = b.GetComponentInChildren<Text>();
								if (bt != null) {
									LocalizedText loc = bt.GetComponent<LocalizedText>();
									if (loc == null) loc = bt.gameObject.AddComponent<LocalizedText>();
									loc.m_localizationKey = qNames[i];
									loc.Refresh();
								}
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
				InjectNewOptions(sm);
			}
		}
	}

	private void InjectNewOptions(SettingsManager sm)
	{
		InputRemapper.Load();
		foreach (var p in new[] { m_graphicsPanel, m_audioPanel, m_gameplayPanel, m_controlsPanel }) {
			if (p == null) continue;
			CleanUI(p.transform);
		}

		if (m_graphicsPanel != null) {
			Transform container = FindBestContainer(m_graphicsPanel.transform, "Injected_Content");
			
			CreateOptionToggle(container, "FULLSCREEN", "FULLSCREEN", (v) => sm.SetFullscreen(v), Screen.fullScreen);
			CreateOptionToggle(container, "VSYNC", "V-SYNC", (v) => sm.SetVSync(v), QualitySettings.vSyncCount > 0);
			CreateOptionSlider(container, "QUALITY", "ENGINE QUALITY", (v) => sm.SetQuality(Mathf.RoundToInt(v * 2)), QualitySettings.GetQualityLevel() / 2f);
			CreateOptionToggle(container, "FPS_Toggle", "SHOW FPS", (v) => sm.ToggleFPS(v), PlayerPrefs.GetInt("ShowFPS", 0) == 1);
		}

		if (m_audioPanel != null) {
			Transform container = FindBestContainer(m_audioPanel.transform, "Injected_Content");
			CreateOptionSlider(container, "MASTER_VOLUME", "MASTER GAIN", (v) => sm.SetVolume(v), PlayerPrefs.GetFloat("MasterVolume", 1f));
		}

		if (m_gameplayPanel != null) {
			Transform container = FindBestContainer(m_gameplayPanel.transform, "Injected_Content");
			CreateOptionSlider(container, "SENSITIVITY", "RESPONSE LAG", (v) => sm.SetSensitivity(Mathf.Lerp(0.1f, 3.0f, v)), (PlayerPrefs.GetFloat("MouseSensitivity", 1.0f) - 0.1f) / 2.9f);
			CreateOptionSlider(container, "FOV", "VIEW ANGLE", (v) => sm.SetFOV(Mathf.Lerp(60f, 110f, v)), (PlayerPrefs.GetFloat("FieldOfView", 70f) - 60f) / 50f);
			CreateOptionToggle(container, "INVERT_Y", "INVERT Y-AXIS", (v) => sm.SetInvertY(v), sm.GetInvertY());
			CreateOptionToggle(container, "LANGUAGE_Toggle", "LANGUAGE", (v) => {
				sm.SetLanguage(v ? 1 : 0);
				InjectNewOptions(sm); 
				RefreshAllLocalizations();
				UpdateTabVisibility(m_currentTab); 
			}, LocalizationManager.CurrentLanguage == LocalizationManager.Language.FR, "FR", "EN");
		}

		if (m_controlsPanel != null) {
			InjectControlsOnly();
		}
	}

	private void InjectControlsOnly()
	{
		if (m_controlsPanel == null) return;
		CleanUI(m_controlsPanel.transform);
		
		CanvasGroup cg = m_controlsPanel.GetComponent<CanvasGroup>();
		if (cg != null) cg.alpha = 1f;
		m_controlsPanel.SetActive(true);

		Transform panelTrans = m_controlsPanel.transform;
		
		var layout = m_controlsPanel.GetComponent<LayoutGroup>();
		if (layout != null) DestroyImmediate(layout);

		GameObject scrollObj = new GameObject("ScrollContainer", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
		scrollObj.transform.SetParent(panelTrans, false);
		scrollObj.transform.SetAsLastSibling();
		RectTransform scrt = scrollObj.GetComponent<RectTransform>();
		scrt.anchorMin = new Vector2(0.1f, 0.08f); scrt.anchorMax = new Vector2(0.95f, 0.78f);
		scrt.offsetMin = Vector2.zero; scrt.offsetMax = Vector2.zero;
		scrt.anchoredPosition = Vector2.zero;
		scrollObj.GetComponent<Image>().color = new Color(0, 0, 0, 0);

		GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
		viewportObj.transform.SetParent(scrollObj.transform, false);
		viewportObj.GetComponent<Image>().color = new Color(0, 0, 0, 0.05f);
		viewportObj.GetComponent<Mask>().showMaskGraphic = false;
		RectTransform vrt = viewportObj.GetComponent<RectTransform>();
		vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
		vrt.offsetMin = Vector2.zero; vrt.offsetMax = new Vector2(-15, 0);

		GameObject contentObj = new GameObject("Controls_Content", typeof(RectTransform));
		contentObj.transform.SetParent(viewportObj.transform, false);
		RectTransform crt = contentObj.GetComponent<RectTransform>();
		crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1);
		crt.pivot = new Vector2(0, 1);
		crt.anchoredPosition = Vector2.zero;
		crt.sizeDelta = new Vector2(0, 1000); 
		contentObj.transform.SetAsLastSibling();

		VerticalLayoutGroup v = contentObj.AddComponent<VerticalLayoutGroup>();
		v.spacing = 4; v.padding = new RectOffset(5, 5, 5, 5);
		v.childControlHeight = true; v.childForceExpandHeight = false;
		v.childControlWidth = true; v.childForceExpandWidth = true;
		v.childAlignment = TextAnchor.UpperLeft;

		contentObj.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		GameObject sbObj = new GameObject("Scrollbar", typeof(RectTransform), typeof(Scrollbar), typeof(Image));
		sbObj.transform.SetParent(scrollObj.transform, false);
		RectTransform sbrt = sbObj.GetComponent<RectTransform>();
		sbrt.anchorMin = new Vector2(1, 0); sbrt.anchorMax = Vector2.one;
		sbrt.pivot = new Vector2(1, 0.5f);
		sbrt.offsetMin = new Vector2(-12, 10); sbrt.offsetMax = new Vector2(0, -10);
		sbObj.GetComponent<Image>().color = new Color(1, 1, 1, 0.2f);
		sbObj.transform.SetAsLastSibling();
		
		Scrollbar sb = sbObj.GetComponent<Scrollbar>();
		sb.direction = Scrollbar.Direction.BottomToTop;
		
		GameObject handleObj = new GameObject("Handle", typeof(RectTransform), typeof(Image));
		handleObj.transform.SetParent(sbObj.transform, false);
		handleObj.GetComponent<Image>().color = new Color(0, 0.8f, 1f, 0.8f);
		StretchFull(handleObj.GetComponent<RectTransform>());
		sb.handleRect = handleObj.GetComponent<RectTransform>();
		handleObj.transform.SetAsLastSibling();

		ScrollRect sr = scrollObj.GetComponent<ScrollRect>();
		sr.viewport = vrt; sr.content = crt;
		sr.verticalScrollbar = sb;
		sr.horizontal = false; sr.vertical = true;
		sr.scrollSensitivity = 35f;
		sr.movementType = ScrollRect.MovementType.Clamped;

		string[] actions = { "FORWARD", "BACKWARD", "LEFT", "RIGHT", "UP", "DOWN", "YAW_LEFT", "YAW_RIGHT", "BOOST", "BRAKE", "TRAJECTORY", "MODIFIER", "AUTOPILOT", "TARGET_ARRIVAL", "TARGET_REFUEL" };
		foreach(var act in actions)
			CreateKeyBindUI(crt, act);

		UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(crt);
		UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(scrt);
		UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(panelTrans.GetComponent<RectTransform>());
	}

	private void CreateOptionSlider(Transform parent, string name, string labelKey, System.Action<float> action, float initialValue)
	{
		if (parent.Find(name) != null) return;
		GameObject go = new GameObject(name, typeof(RectTransform));
		go.transform.SetParent(parent, false);
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.sizeDelta = new Vector2(450, 40);

		Text t = CreateTextWithLocalized(go.transform, labelKey, 14, Color.white, TextAnchor.MiddleLeft);
		RectTransform lrt = t.GetComponent<RectTransform>();
		lrt.anchorMin = new Vector2(0.05f, 0); lrt.anchorMax = new Vector2(0.5f, 1);
		lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
		
		GameObject sliderObj = new GameObject("Slider", typeof(RectTransform));
		sliderObj.transform.SetParent(go.transform, false);
		RectTransform srt = sliderObj.GetComponent<RectTransform>();
		srt.anchorMin = new Vector2(0.6f, 0.5f); srt.anchorMax = new Vector2(0.95f, 0.5f);
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

	private Transform FindBestContainer(Transform panel, string targetName)
	{
		Transform t = panel.Find(targetName);
		if (t == null) t = FindChildRecursive(panel, targetName);
		if (t != null) return t;

		t = panel.Find("Content");
		if (t != null) return t;

		Transform existingInjected = panel.Find("Injected_Content");
		if (existingInjected != null) return existingInjected;

		GameObject go = new GameObject("Injected_Content", typeof(RectTransform));
		go.transform.SetParent(panel, false);
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.anchorMin = new Vector2(0.5f, 0.1f); rt.anchorMax = new Vector2(0.95f, 0.85f);
		rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
		
		VerticalLayoutGroup v = go.AddComponent<VerticalLayoutGroup>();
		v.spacing = 10; 
		v.childControlHeight = false; v.childForceExpandHeight = false;
		v.childControlWidth = true; v.childForceExpandWidth = true;
		v.childAlignment = TextAnchor.UpperLeft;

		return go.transform;
	}

	private void CreateOptionToggle(Transform parent, string name, string labelKey, System.Action<bool> action, bool initialValue, string onLabelOverride = null, string offLabelOverride = null)
	{
		if (parent.Find(name) != null) return;
		GameObject go = new GameObject(name, typeof(RectTransform));
		go.transform.SetParent(parent, false);
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.sizeDelta = new Vector2(450, 40);

		Text t = CreateTextWithLocalized(go.transform, labelKey, 14, Color.white, TextAnchor.MiddleLeft);
		RectTransform lrt = t.GetComponent<RectTransform>();
		lrt.anchorMin = new Vector2(0.05f, 0); lrt.anchorMax = new Vector2(0.5f, 1);
		lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
		
		GameObject btnObj = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Button));
		btnObj.transform.SetParent(go.transform, false);
		RectTransform brt = btnObj.GetComponent<RectTransform>();
		brt.anchorMin = new Vector2(0.65f, 0.5f); brt.anchorMax = new Vector2(0.95f, 0.5f);
		brt.sizeDelta = new Vector2(0, 30);
		brt.anchoredPosition = Vector2.zero;

		Image img = btnObj.GetComponent<Image>();
		img.color = initialValue ? new Color(0, 0.8f, 1f, 0.4f) : new Color(0, 0.1f, 0.2f, 0.6f);
		img.raycastTarget = true;
		
		Button b = btnObj.GetComponent<Button>();
		GameObject valObj = new GameObject("Value", typeof(RectTransform));
		valObj.transform.SetParent(btnObj.transform, false);
		Text vt = valObj.AddComponent<Text>();
		
		bool state = initialValue;
		
		if (onLabelOverride == null && offLabelOverride == null)
		{
			LocalizedText loc = valObj.AddComponent<LocalizedText>();
			loc.m_localizationKey = state ? "ON" : "OFF";
			loc.Refresh();
		}
		else
			vt.text = state ? (onLabelOverride ?? "ON") : (offLabelOverride ?? "OFF");

		vt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); vt.fontSize = 14; vt.color = Color.white;
		vt.alignment = TextAnchor.MiddleCenter;
		vt.raycastTarget = false;
		StretchFull(valObj.GetComponent<RectTransform>());

		b.onClick.AddListener(() => {
			state = !state;
			img.color = state ? new Color(0, 0.8f, 1f, 0.4f) : new Color(0, 0.1f, 0.2f, 0.6f);
			
			if (onLabelOverride == null && offLabelOverride == null)
			{
				LocalizedText loc = valObj.GetComponent<LocalizedText>();
				if (loc) { loc.m_localizationKey = state ? "ON" : "OFF"; loc.Refresh(); }
			}
			else
				vt.text = state ? (onLabelOverride ?? "ON") : (offLabelOverride ?? "OFF");
			
			action(state);
		});
	}

	private void CreateKeyBindUI(Transform parent, string actionName)
	{
		if (parent.Find("Bind_" + actionName) != null) return;
		GameObject go = new GameObject("Bind_" + actionName, typeof(RectTransform), typeof(LayoutElement), typeof(Image));
		go.transform.SetParent(parent, false);
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.sizeDelta = new Vector2(450, 40);
		
		Image rowImg = go.GetComponent<Image>();
		rowImg.color = new Color(0, 0, 0, 0); 
		
		LayoutElement le = go.GetComponent<LayoutElement>();
		le.minHeight = 32; le.preferredHeight = 32;

		Text t = CreateTextWithLocalized(go.transform, actionName, 13, Color.white, TextAnchor.MiddleLeft);
		RectTransform trt = t.GetComponent<RectTransform>();
		trt.anchorMin = new Vector2(0.05f, 0); trt.anchorMax = new Vector2(0.5f, 1);
		trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
		
		GameObject btnObj = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
		btnObj.transform.SetParent(go.transform, false);
		Image img = btnObj.GetComponent<Image>();
		img.color = new Color(0, 0.8f, 1f, 0.2f);
		img.raycastTarget = true;
		RectTransform brt = btnObj.GetComponent<RectTransform>();
		brt.anchorMin = new Vector2(0.6f, 0.5f); brt.anchorMax = new Vector2(0.88f, 0.5f); 
		brt.sizeDelta = new Vector2(0, 26);
		brt.anchoredPosition = Vector2.zero;
		brt.offsetMin = new Vector2(brt.offsetMin.x, -13); brt.offsetMax = new Vector2(brt.offsetMax.x, 13);

		Text kt = CreateTextWithLocalized(btnObj.transform, InputRemapper.Binds[actionName].ToString(), 16, Color.cyan, TextAnchor.MiddleCenter, false);
		kt.raycastTarget = false;
		StretchFull(kt.GetComponent<RectTransform>());

		Button b = btnObj.GetComponent<Button>();
		b.onClick.AddListener(() => {
			kt.text = "...";
			StartCoroutine(WaitForKey(actionName, kt));
		});

		GameObject resObj = new GameObject("Reset", typeof(RectTransform), typeof(Image), typeof(Button));
		resObj.transform.SetParent(go.transform, false);
		RectTransform rrt = resObj.GetComponent<RectTransform>();
		rrt.anchorMin = new Vector2(0.91f, 0.5f); rrt.anchorMax = new Vector2(0.98f, 0.5f);
		rrt.sizeDelta = new Vector2(0, 26);
		rrt.anchoredPosition = Vector2.zero;
		rrt.offsetMin = new Vector2(rrt.offsetMin.x, -13); rrt.offsetMax = new Vector2(rrt.offsetMax.x, 13);

		Image rImg = resObj.GetComponent<Image>();
		rImg.color = new Color(1f, 0.3f, 0.3f, 0.15f);
		
		Text resTxt = CreateTextWithLocalized(resObj.transform, "⟳", 16, Color.white, TextAnchor.MiddleCenter);
		StretchFull(resTxt.GetComponent<RectTransform>());

		Button rb = resObj.GetComponent<Button>();
		rb.onClick.AddListener(() => {
			InputRemapper.ResetBind(actionName);
			kt.text = InputRemapper.Binds[actionName].ToString();
		});
	}

	private Text CreateTextWithLocalized(Transform p, string textKey, int fontSize, Color color, TextAnchor alignment, bool localized = true) {
		GameObject g = new GameObject("Text"); g.transform.SetParent(p, false);
		Text t = g.AddComponent<Text>();
		t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		t.fontSize = fontSize;
		t.color = color;
		t.alignment = alignment;
		t.horizontalOverflow = HorizontalWrapMode.Overflow;
		t.verticalOverflow = VerticalWrapMode.Overflow;
		t.text = textKey;
		if (localized) {
			g.AddComponent<LocalizedText>().m_localizationKey = textKey;
		}
		return t;
	}

	private IEnumerator WaitForKey(string actionName, Text display) {
		string originalText = InputRemapper.Binds[actionName].ToString();
		yield return new WaitForSecondsRealtime(0.1f);
		bool pressed = false;
		while (!pressed) {
			var kb = UnityEngine.InputSystem.Keyboard.current;
			if (kb != null && kb.anyKey.wasPressedThisFrame) {
				foreach(var keyControl in kb.allKeys) {
					if (keyControl.wasPressedThisFrame) {
						UnityEngine.InputSystem.Key k = keyControl.keyCode;
						if (k == UnityEngine.InputSystem.Key.Escape) {
							display.text = "ESC BLOCKED";
							yield return new WaitForSecondsRealtime(0.5f);
							display.text = "...";
							continue;
						}
						
						bool alreadyUsed = false;
						foreach(var bind in InputRemapper.Binds) {
							if (bind.Value == k && bind.Key != actionName)
								alreadyUsed = true; break;
						}
						
						if (alreadyUsed) {
							display.text = "USED!";
							yield return new WaitForSecondsRealtime(0.5f);
							display.text = "...";
							continue;
						}

						InputRemapper.SetBind(actionName, k);
						display.text = k.ToString();
						pressed = true; break;
					}
				}
			}
			yield return null;
		}
	}

	private void CleanUI(Transform parent) {
		List<GameObject> toDestroy = new List<GameObject>();
		FindObjectsToDestroy(parent, toDestroy);
		foreach(var go in toDestroy) DestroyImmediate(go);
	}

	private void FindObjectsToDestroy(Transform p, List<GameObject> list) {
		foreach(Transform child in p) {
			string n = child.name;
			if (n == "Injected_Content" || n == "ScrollContainer" || n.StartsWith("Bind_") || n == "Separator")
				list.Add(child.gameObject);
			else 
				FindObjectsToDestroy(child, list);
		}
	}
	
	private void StretchFull(RectTransform rt) {
		rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
	}

	private void ReconnectButton(Transform root, string name, UnityEngine.Events.UnityAction action) {
		Transform t = root.Find(name);
		if (t == null) t = FindChildRecursive(root, name);
		if (t != null) {
			Button b = t.GetComponent<Button>();
			if (b == null) b = t.GetComponentInChildren<Button>();
			if (b != null) {
				b.onClick.RemoveAllListeners();
				b.onClick.AddListener(() => {
					action();
				});
				Image img = b.GetComponent<Image>();
				if (img) img.raycastTarget = true;
			}
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
			
			LocalizedText loc = valT.GetComponent<LocalizedText>();
			if (loc == null) loc = valT.gameObject.AddComponent<LocalizedText>();
			
			bool initialState = (valT.text == "ON" || valT.text == "ACTIF");
			loc.m_localizationKey = initialState ? "ON" : "OFF";
			loc.Refresh();

			b.onClick.AddListener(() => {
				bool current = (valT.text == LocalizationManager.Get("OFF"));
				loc.m_localizationKey = current ? "ON" : "OFF";
				loc.Refresh();
				
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
		HashSet<string> seen = new HashSet<string>();
		Resolution[] res = Screen.resolutions;
		for (int i = res.Length - 1; i >= 0; i--) {
			string s = res[i].width + "x" + res[i].height;
			if (!seen.Contains(s)) {
				options.Insert(0, s);
				seen.Add(s);
			}
		}
		if (options.Count == 0) options.Add(Screen.width + "x" + Screen.height);
		return options;
	}

	private int GetCurrentResolutionIndex() {
		var options = GetResolutionOptions();
		string current = Screen.width + "x" + Screen.height;
		for (int i = 0; i < options.Count; i++) if (options[i] == current) return i;
		return options.Count - 1;
	}

	private string m_currentTab = "GRAPHICS";

	public void SelectTabAtRuntime(string tabName) { 
		
		m_currentTab = tabName; 
		UpdateTabVisibility(tabName); 
	}

	private void UpdateTabVisibility(string tabName)
	{
		if (m_optionsMenuPanel == null) return;

		Transform root = m_optionsMenuPanel.transform.Find("OptionsRoot");
		if (root == null) root = m_optionsMenuPanel.transform;

		foreach (Transform child in root) {
			string n = child.name.ToUpper();
			
			if (n.Contains("SIDEBAR") || n.Contains("FRAME") || n.Contains("BACKGROUND") || n == "TITLE" || n == "HEADER") {
				child.gameObject.SetActive(true);
				continue;
			}
			
			bool isSelected = false;
			if (tabName == "GRAPHICS" && (n.Contains("GRAPHIC") || n.Contains("DISPLAY") || child.gameObject == m_graphicsPanel)) isSelected = true;
			else if (tabName == "AUDIO" && (n.Contains("AUDIO") || child.gameObject == m_audioPanel)) isSelected = true;
			else if (tabName == "GAMEPLAY" && (n.Contains("GAMEPLAY") || n.Contains("INTERFACE") || child.gameObject == m_gameplayPanel)) isSelected = true;
			else if (tabName == "CONTROLS" && (n.Contains("CONTROL") || n.Contains("MAPPING") || child.gameObject == m_controlsPanel)) isSelected = true;

			child.gameObject.SetActive(isSelected);
			
			if (isSelected) {
				foreach (Transform c in child) {
					string cn = c.name.ToUpper();
					if (cn.Contains("TITLE") || cn.Contains("HEADER") || cn.Contains("BACKGROUND") || 
						cn.Contains("INJECTED") || cn.Contains("SCROLL") || cn.Contains("CONTENT") ||
						(c.GetComponentInChildren<Text>() != null && c.localPosition.y > 100)) continue;
					c.gameObject.SetActive(false);
				}

				if (tabName == "GRAPHICS") m_graphicsPanel = child.gameObject;
				else if (tabName == "AUDIO") m_audioPanel = child.gameObject;
				else if (tabName == "GAMEPLAY") m_gameplayPanel = child.gameObject;
				else if (tabName == "CONTROLS") {
					m_controlsPanel = child.gameObject;
					InjectControlsOnly(); 
				}
				UpdateTitleText(child, tabName);
			}
		}

		Transform tabs = root.Find("Sidebar/TabsContainer");
		if (tabs == null) tabs = m_optionsMenuPanel.transform; 
		
		if (tabs != null) {
			foreach (Transform t in tabs) {
				if (!t.name.StartsWith("Tab_") && !t.name.Contains("Tab")) continue;
				
				bool isActive = (t.name == "Tab_" + tabName || t.name.ToUpper().EndsWith(tabName));
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
				
				Text txt = t.GetComponentInChildren<Text>();
				if (txt) {
					txt.color = isActive ? Color.white : new Color(1, 1, 1, 0.3f);
					LocalizedText loc = t.GetComponentInChildren<LocalizedText>();
					if (loc == null) loc = t.gameObject.AddComponent<LocalizedText>();
					if (loc != null) {
						if (string.IsNullOrEmpty(loc.m_localizationKey))
							loc.m_localizationKey = t.name.Replace("Tab_", "").ToUpper();
						txt.text = LocalizationManager.Get(loc.m_localizationKey);
					}
				}
			}
		}
	}

	private void UpdateTitleText(Transform panel, string tabName) {
		Text titleText = null;
		Transform tObj = FindChildRecursive(panel, "Title") ?? FindChildRecursive(panel, "Header");
		if (tObj == null) tObj = panel.Find("Title") ?? panel.Find("Header") ?? panel.Find("Header_Label");
		
		if (tObj != null) titleText = tObj.GetComponentInChildren<Text>();
		if (titleText == null) {
			foreach(Text txt in panel.GetComponentsInChildren<Text>(true)) {
				if (txt.transform.localPosition.y > 100) { titleText = txt; break; }
			}
		}
		
		if (titleText != null) {
			titleText.gameObject.SetActive(true);
			bool isFR = LocalizationManager.CurrentLanguage == LocalizationManager.Language.FR;
			
			if (tabName == "GRAPHICS") titleText.text = isFR ? "> PROTOCOLE D'AFFICHAGE" : "> DISPLAY PROTOCOL";
			else if (tabName == "AUDIO") titleText.text = isFR ? "> PROTOCOLE AUDIO" : "> AUDIO PROTOCOL";
			else if (tabName == "GAMEPLAY") titleText.text = isFR ? "> INTERFACE NEURALE" : "> NEURAL INTERFACE";
			else if (tabName == "CONTROLS") titleText.text = isFR ? "> CONFIGURATION DES COMMANDES" : "> CONTROL MAPPING";
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
