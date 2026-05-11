using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class LocalizedText : MonoBehaviour
{
	public string m_localizationKey;
	private Text m_text;

	void Awake()
	{
		m_text = GetComponent<Text>();
		if (string.IsNullOrEmpty(m_localizationKey) && m_text != null) 
			m_localizationKey = m_text.text.ToUpper();
	}

	void OnEnable()
	{
		Refresh();
	}

	void Start()
	{
		Refresh();
	}

	public void Refresh()
	{
		if (m_text == null) m_text = GetComponent<Text>();
		if (m_text != null && !string.IsNullOrEmpty(m_localizationKey))
		{
			m_text.text = LocalizationManager.Get(m_localizationKey);
		}
	}
}
