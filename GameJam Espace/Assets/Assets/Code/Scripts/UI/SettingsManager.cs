using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SettingsManager : MonoBehaviour
{
	[Header("UI Elements")]
	public Slider m_sensitivitySlider;
	public ShipControl m_playerShip;

	private const string SENSITIVITY_KEY = "MouseSensitivity";
	private const string VOLUME_KEY = "MasterVolume";
	private const string QUALITY_KEY = "GraphicsQuality";
	private const string RESOLUTION_KEY = "ResolutionIndex";
	private const string FULLSCREEN_KEY = "Fullscreen";
	private const string VSYNC_KEY = "VSync";
	private const string FOV_KEY = "FieldOfView";
	private const string INVERTY_KEY = "InvertY";

	private void Start()
	{
		ApplyAllSettings();
	}

	public void ApplyAllSettings()
	{
		float savedSensitivity = PlayerPrefs.GetFloat(SENSITIVITY_KEY, 1.0f);
		ApplySensitivity(savedSensitivity);

		float savedVolume = PlayerPrefs.GetFloat(VOLUME_KEY, 0.8f);
		SetVolume(savedVolume);

		int savedQuality = PlayerPrefs.GetInt(QUALITY_KEY, QualitySettings.GetQualityLevel());
		SetQuality(savedQuality);

		float savedFOV = PlayerPrefs.GetFloat(FOV_KEY, 70f);
		ApplyFOV(savedFOV);

		bool savedInvertY = PlayerPrefs.GetInt(INVERTY_KEY, 0) == 1;
		ApplyInvertY(savedInvertY);

		int resIndex = PlayerPrefs.GetInt(RESOLUTION_KEY, -1);
		bool isFullscreen = PlayerPrefs.GetInt(FULLSCREEN_KEY, Screen.fullScreen ? 1 : 0) == 1;
		int vsync = PlayerPrefs.GetInt(VSYNC_KEY, QualitySettings.vSyncCount > 0 ? 1 : 0);
		
		ApplyDisplaySettings(resIndex, isFullscreen, vsync);
	}

	public void SetSensitivity(float value)
	{
		PlayerPrefs.SetFloat(SENSITIVITY_KEY, value);
		ApplySensitivity(value);
	}

	private void ApplySensitivity(float sensitivity)
	{
		if (m_playerShip != null)
			m_playerShip.m_mouse_sensitivity = sensitivity;
	}

	public void SetVolume(float value)
	{
		PlayerPrefs.SetFloat(VOLUME_KEY, value);
		AudioListener.volume = value;
	}

	public void SetQuality(int index)
	{
		PlayerPrefs.SetInt(QUALITY_KEY, index);
		QualitySettings.SetQualityLevel(index);
	}

	public void SetFOV(float value)
	{
		PlayerPrefs.SetFloat(FOV_KEY, value);
		ApplyFOV(value);
	}

	private void ApplyFOV(float fov)
	{
		Camera mainCam = Camera.main;
		if (mainCam != null)
			mainCam.fieldOfView = fov;
	}

	public void SetInvertY(bool invert)
	{
		PlayerPrefs.SetInt(INVERTY_KEY, invert ? 1 : 0);
		ApplyInvertY(invert);
	}

	private void ApplyInvertY(bool invert)
	{
		
	}

	public void SetVSync(bool enabled)
	{
		PlayerPrefs.SetInt(VSYNC_KEY, enabled ? 1 : 0);
		QualitySettings.vSyncCount = enabled ? 1 : 0;
	}

	public void SetFullscreen(bool enabled)
	{
		PlayerPrefs.SetInt(FULLSCREEN_KEY, enabled ? 1 : 0);
		Screen.fullScreen = enabled;
	}

	public void SetResolution(int index)
	{
		PlayerPrefs.SetInt(RESOLUTION_KEY, index);
		Resolution[] resolutions = Screen.resolutions;
		if (index >= 0 && index < resolutions.Length)
			Screen.SetResolution(resolutions[index].width, resolutions[index].height, Screen.fullScreen);
	}

	private void ApplyDisplaySettings(int resIndex, bool fullscreen, int vsync)
	{
		QualitySettings.vSyncCount = vsync;
		
		if (resIndex != -1)
		{
			Resolution[] resolutions = Screen.resolutions;
			if (resIndex < resolutions.Length)
				Screen.SetResolution(resolutions[resIndex].width, resolutions[resIndex].height, fullscreen);
		}
		else
			Screen.fullScreen = fullscreen;
	}

	public bool GetInvertY()
	{
		return PlayerPrefs.GetInt(INVERTY_KEY, 0) == 1;
	}
}

