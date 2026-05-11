using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;


public class UIButtonEffects : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
	[Header("Animation Settings")]
	public float hoverScale = 1.01f;
	public float transitionSpeed = 15f;
		
	[Header("Visual References")]
	public Image backgroundImage;
	public Text buttonText;
		
	[Header("Colors")]
	public Color normalColor = new Color(0.5f, 0.8f, 1f, 0.5f);
	public Color hoverColor = new Color(0.5f, 0.8f, 1f, 1f);
	public Color pressedColor = new Color(1f, 0.5f, 0f, 1f);

	private Vector3 originalScale;
	private Vector3 targetScale;
	private Color targetColor;

	private Component tmpText;

	private void Start()
	{
		originalScale = transform.localScale;
		targetScale = originalScale;
		
		if (backgroundImage == null) backgroundImage = GetComponent<Image>();
		if (buttonText == null) buttonText = GetComponentInChildren<Text>();
		
		foreach (var comp in GetComponentsInChildren<Component>())
		{
			if (comp.GetType().Name.Contains("TextMeshPro"))
			{
				tmpText = comp;
				break;
			}
		}
		
		if (backgroundImage != null && normalColor.a == 0.5f && normalColor.r == 0.5f) {
			normalColor = backgroundImage.color;
			Color.RGBToHSV(normalColor, out float h, out float s, out float v);
			hoverColor = Color.HSVToRGB(h, s, Mathf.Clamp01(v + 0.3f));
			hoverColor.a = Mathf.Min(1f, normalColor.a * 1.5f);
			pressedColor = new Color(0, 0.8f, 1f, Mathf.Min(1f, normalColor.a * 2f));
		}

		targetColor = normalColor;
	}

	private void Update()
	{
		transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * transitionSpeed);
		
		if (backgroundImage != null)
			backgroundImage.color = Color.Lerp(backgroundImage.color, targetColor, Time.unscaledDeltaTime * transitionSpeed);
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		targetScale = originalScale * hoverScale;
		targetColor = hoverColor;
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		targetScale = originalScale;
		targetColor = normalColor;
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		targetScale = originalScale * 0.95f;
		targetColor = pressedColor;
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		targetScale = originalScale * hoverScale;
		targetColor = hoverColor;
	}
}
