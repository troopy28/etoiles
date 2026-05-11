using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class GameProgressManager : MonoBehaviour
{
	public static GameProgressManager Instance;

	[Header("Settings")]
	public float m_collisionPadding = 5f;
	public float m_victoryDistance = 150f;

	[Header("UI References")]
	public GameObject m_statusPanel;
	public Text m_statusTitle;
	public Text m_statusDescription;
	public CanvasGroup m_panelGroup;

	[Header("Briefing UI")]
	public GameObject m_briefingPanel;
	public Text m_briefingTitle;
	public Text m_briefingContent;
	public CanvasGroup m_briefingGroup;

	private ShipControl m_player;
	private SimGravityBody m_destinationBody;
	private PauseMenuManager m_pauseManager;

	private bool m_gameEnded = false;
	private bool m_briefingActive = true;

	public bool IsGameEnded => m_gameEnded;
	public bool IsBriefingActive => m_briefingActive;

	void Awake()
	{
		Instance = this;
		if (m_statusPanel == null) CreateUI();
	}

	private IEnumerator Start()
	{
		m_player = FindAnyObjectByType<ShipControl>();
		m_pauseManager = FindAnyObjectByType<PauseMenuManager>();

		if (m_pauseManager == null)
		{
			GameObject pmGO = new GameObject("PauseMenuManager");
			m_pauseManager = pmGO.AddComponent<PauseMenuManager>();
		}

		FindDestination();
		
		if (m_statusPanel) m_statusPanel.SetActive(false);
		
		yield return null;
		ShowBriefing();
	}

	private void FindDestination()
	{
		var bodies = SimGravityBody.AllRegistered;
		foreach (var b in bodies)
		{
			if (b && b.is_destination)
			{
				m_destinationBody = b;
				Debug.Log($"[GameProgress] Destination identifiée : {b.name} à {b.transform.position}");
				break;
			}
		}
	}

	void ShowBriefing()
	{
		Time.timeScale = 0f;
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		
		if (m_briefingPanel == null) CreateBriefingUI();
		m_briefingPanel.SetActive(true);
		m_briefingGroup.alpha = 1f;

		if (m_player == null) m_player = FindAnyObjectByType<ShipControl>();
		if (m_player != null) m_player.m_show_hud = false;
		
		m_briefingContent.text = LocalizationManager.Get("BRIEFING_BODY");
	}

	public void StartMission()
	{
		m_briefingActive = false;
		m_briefingPanel.SetActive(false);
		Time.timeScale = 1f;

		if (m_player != null) m_player.m_show_hud = true;

		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

	void Update()
	{
		if (m_gameEnded) return;
		if (m_briefingActive) return;

		if (m_pauseManager != null && m_pauseManager.IsPaused) return;

		if (m_player == null)
		{
			m_player = FindAnyObjectByType<ShipControl>();
			if (m_player == null) 
			{
				if (Time.frameCount % 60 == 0) Debug.LogWarning("[GameProgress] ShipControl non trouvé dans la scène.");
				return;
			}
		}

		if (m_destinationBody == null) FindDestination();

		if (m_destinationBody != null)
		{
			float distToEnd = Vector3.Distance(m_player.transform.position, m_destinationBody.transform.position);
			float destRadius = m_destinationBody.visual_radius > 0 ? m_destinationBody.visual_radius : m_destinationBody.transform.localScale.x * 0.5f;
			
			if (distToEnd < destRadius + m_victoryDistance)
			{
				Debug.Log($"[GameProgress] Victoire atteinte : distance {distToEnd}");
				Win();
				return;
			}
		}

		if (m_player.IsOverheating)
		{
			Die("Critical hull overheating");
			return;
		}

		if (m_player.m_fuel <= 0f && !m_player.IsRefueling)
		{
			Die("Total energy reserves depleted");
			return;
		}

		var bodies = SimGravityBody.AllRegistered;
		Vector3 playerPos = m_player.transform.position;

		for (int i = 0; i < bodies.Count; i++)
		{
			var body = bodies[i];
			if (body == null || body.gameObject == m_player.gameObject) continue;
			if (body.is_start_point || body.is_destination) continue;

			float bodyRadius = body.visual_radius > 0 ? body.visual_radius : body.transform.localScale.x * 0.5f;
			float dist = Vector3.Distance(playerPos, body.transform.position);

			if (dist < bodyRadius + m_collisionPadding)
			{
				Die(body.name);
				break;
			}
		}
	}

	public void Die(string reason)
	{
		if (m_gameEnded) return;
		m_gameEnded = true;

		if (m_pauseManager != null && m_pauseManager.IsPaused)
			m_pauseManager.Resume();

		if (m_briefingPanel != null) m_briefingPanel.SetActive(false);

		m_statusTitle.text = LocalizationManager.Get("MISSION FAILED");
		m_statusTitle.color = new Color(1f, 0.2f, 0.2f);
		
		if (reason.StartsWith("Cargo Destruction"))
			m_statusDescription.text = LocalizationManager.Get("Cargo has been destroyed: ") + reason.Replace("Cargo Destruction ", "");
		else if (reason.Contains("impact") || reason.Contains("overheating") || reason.Contains("energy"))
			m_statusDescription.text = LocalizationManager.Get(reason);
		else
			m_statusDescription.text = LocalizationManager.Get("Your ship was lost: collision with ") + reason;
		
		LocalizedText loc = m_statusDescription.GetComponent<LocalizedText>();
		if (loc != null) loc.enabled = false;
		
		StartCoroutine(EndGameRoutine());
	}

	public void Win()
	{
		if (m_gameEnded) return;
		m_gameEnded = true;

		if (m_pauseManager != null && m_pauseManager.IsPaused)
			m_pauseManager.Resume();

		if (m_briefingPanel != null) m_briefingPanel.SetActive(false);

		m_statusTitle.text = LocalizationManager.Get("DELIVERY SUCCESSFUL");
		m_statusTitle.color = new Color(0.2f, 1f, 0.5f);
		m_statusDescription.text = LocalizationManager.Get("STABILITY MAINTAINED");
		
		StartCoroutine(EndGameRoutine());
	}

	private IEnumerator EndGameRoutine()
	{
		if (m_player) m_player.enabled = false;
		
		yield return new WaitForSecondsRealtime(1f);

		m_statusPanel.SetActive(true);
		float elapsed = 0;
		while (elapsed < 0.5f)
		{
			elapsed += Time.unscaledDeltaTime;
			m_panelGroup.alpha = elapsed / 0.5f;
			yield return null;
		}

		Time.timeScale = 0f;
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		
		if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
	}

	public void Restart()
	{
		Time.timeScale = 1f;
		SceneManager.LoadScene(SceneManager.GetActiveScene().name);
	}

	public void BackToMenu()
	{
		Time.timeScale = 1f;
		SceneManager.LoadScene("MainMenu");
	}

	private void CreateBriefingUI()
	{
		GameObject canvasObj = GameObject.Find("ProgressCanvas");
		if (canvasObj == null) canvasObj = CreateCanvas();

		m_briefingPanel = new GameObject("BriefingPanel", typeof(RectTransform));
		m_briefingPanel.transform.SetParent(canvasObj.transform, false);
		m_briefingGroup = m_briefingPanel.AddComponent<CanvasGroup>();
		StretchFull(m_briefingPanel.GetComponent<RectTransform>());

		Image overlay = m_briefingPanel.AddComponent<Image>();
		overlay.color = new Color(0, 0, 0, 0.98f);
		overlay.raycastTarget = true;

		GameObject card = new GameObject("BriefingCard", typeof(RectTransform));
		card.transform.SetParent(m_briefingPanel.transform, false);
		RectTransform cardRT = card.GetComponent<RectTransform>();
		cardRT.sizeDelta = new Vector2(1400, 900);
		
		Image cardImg = card.AddComponent<Image>();
		cardImg.color = new Color(0.01f, 0.02f, 0.04f, 1f);
		
		GameObject border = new GameObject("Border", typeof(RectTransform));
		border.transform.SetParent(card.transform, false);
		StretchFull(border.GetComponent<RectTransform>());
		Image borderImg = border.AddComponent<Image>();
		borderImg.color = new Color(0f, 0.8f, 1f, 0.1f);
		border.AddComponent<Outline>().effectColor = new Color(0, 0.8f, 1f, 0.8f);
		borderImg.rectTransform.offsetMin = new Vector2(-2, -2);
		borderImg.rectTransform.offsetMax = new Vector2(2, 2);

		GameObject content = new GameObject("Content", typeof(RectTransform));
		content.transform.SetParent(card.transform, false);
		StretchFull(content.GetComponent<RectTransform>());
		
		VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
		vlg.childAlignment = TextAnchor.UpperCenter;
		vlg.spacing = 40;
		vlg.padding = new RectOffset(0, 0, 0, 50);
		vlg.childControlHeight = false; vlg.childControlWidth = false;
		vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = false;

		GameObject header = new GameObject("Header", typeof(RectTransform));
		header.transform.SetParent(content.transform, false);
		header.AddComponent<Image>().color = new Color(0f, 0.8f, 1f, 1f);
		header.GetComponent<RectTransform>().sizeDelta = new Vector2(1400, 100);

		m_briefingTitle = CreateText(header.transform, "BRIEFING_TITLE", 38, Color.black);
		m_briefingTitle.fontStyle = FontStyle.Bold;
		StretchFull(m_briefingTitle.GetComponent<RectTransform>());

		GameObject body = new GameObject("Body", typeof(RectTransform));
		body.transform.SetParent(content.transform, false);
		body.GetComponent<RectTransform>().sizeDelta = new Vector2(1200, 500);
		
		m_briefingContent = CreateText(body.transform, "", 28, new Color(0.9f, 0.95f, 1f));
		m_briefingContent.alignment = TextAnchor.UpperLeft;
		m_briefingContent.lineSpacing = 1.3f;
		StretchFull(m_briefingContent.GetComponent<RectTransform>());
		
		Text footerTxt = CreateText(content.transform, "BRIEFING_FOOTER", 14, new Color(0f, 0.8f, 1f, 0.4f));
		footerTxt.GetComponent<RectTransform>().sizeDelta = new Vector2(1200, 30);

		CreateButton(content.transform, "INITIALIZE_SYSTEM", StartMission);
	}

	private GameObject CreateCanvas()
	{
		GameObject canvasObj = new GameObject("ProgressCanvas", typeof(RectTransform));
		Canvas canvas = canvasObj.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 999;
		CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);
		scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
		scaler.matchWidthOrHeight = 0.5f;

		canvasObj.AddComponent<GraphicRaycaster>();

		if (FindAnyObjectByType<EventSystem>() == null)
		{
			GameObject es = new GameObject("EventSystem");
			es.AddComponent<EventSystem>();
			es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
		}

		return canvasObj;
	}

	private void StretchFull(RectTransform rt)
	{
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
	}

	private void CreateUI()
	{
		GameObject canvasObj = GameObject.Find("ProgressCanvas");
		if (canvasObj == null) canvasObj = CreateCanvas();

		m_statusPanel = new GameObject("StatusPanel", typeof(RectTransform));
		m_statusPanel.transform.SetParent(canvasObj.transform, false);
		m_panelGroup = m_statusPanel.AddComponent<CanvasGroup>();
		m_panelGroup.alpha = 0f;

		Image bg = m_statusPanel.AddComponent<Image>();
		bg.color = new Color(0, 0, 0, 0.85f);
		bg.raycastTarget = true;
		StretchFull(bg.rectTransform);

		GameObject content = new GameObject("Content", typeof(RectTransform));
		content.transform.SetParent(m_statusPanel.transform, false);
		StretchFull(content.GetComponent<RectTransform>());
		VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
		vlg.childAlignment = TextAnchor.MiddleCenter;
		vlg.spacing = 30;
		vlg.childControlHeight = false; vlg.childControlWidth = false;
		vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = false;

		m_statusTitle = CreateText(content.transform, "STATUS", 60, Color.white);
		m_statusDescription = CreateText(content.transform, "Description...", 20, Color.gray);
		
		CreateButton(content.transform, "RESTART MISSION", Restart, new Color(0.2f, 0.8f, 0.3f));
		CreateButton(content.transform, "ABANDON TO HANGAR", BackToMenu, new Color(1f, 0.3f, 0.3f));
	}

	private Text CreateText(Transform parent, string txt, int size, Color color)
	{
		GameObject g = new GameObject("Text");
		g.transform.SetParent(parent, false);
		Text t = g.AddComponent<Text>();
		t.text = txt;
		t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		t.fontSize = size;
		t.color = color;
		t.alignment = TextAnchor.MiddleCenter;
		g.GetComponent<RectTransform>().sizeDelta = new Vector2(800, 100);

		var loc = g.AddComponent<LocalizedText>();
		loc.m_localizationKey = txt;

		return t;
	}

	private void CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction action, Color? bgColor = null)
	{
		GameObject g = new GameObject("Button_" + label, typeof(RectTransform));
		g.transform.SetParent(parent, false);
		Image img = g.AddComponent<Image>();
		img.color = bgColor ?? new Color(0, 0.8f, 1f, 1f);
		
		RectTransform rt = g.GetComponent<RectTransform>();
		rt.anchorMin = new Vector2(0.5f, 0.5f);
		rt.anchorMax = new Vector2(0.5f, 0.5f);
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.sizeDelta = new Vector2(600, 100);

		Text t = CreateText(g.transform, label, 32, Color.black);
		t.fontStyle = FontStyle.Bold;
		StretchFull(t.GetComponent<RectTransform>());
		
		Button b = g.AddComponent<Button>();
		ColorBlock cb = b.colors;
		cb.normalColor = Color.white;
		cb.highlightedColor = new Color(0.8f, 0.9f, 1f, 1f);
		cb.pressedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
		b.colors = cb;
		b.onClick.AddListener(action);
	}
}
