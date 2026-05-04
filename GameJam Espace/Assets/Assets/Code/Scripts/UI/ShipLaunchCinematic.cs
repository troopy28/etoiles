using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public static class ShipLaunchCinematic
{
	public static void StartLaunch(MonoBehaviour runner, Transform shipTransform, CanvasGroup menuCanvasGroup, Transform mainCameraTransform)
	{
		runner.StartCoroutine(SafeLaunch(shipTransform, menuCanvasGroup, mainCameraTransform));
	}

	// Wrap the cinematic so any exception in build (where exceptions are silent) still
	// transitions to Galaxie. Without this, a stripped shader / null reference in the
	// flame setup could swallow the routine and leave the player stuck on the menu.
	private static IEnumerator SafeLaunch(Transform shipTransform, CanvasGroup menuCanvasGroup, Transform mainCameraTransform)
	{
		var inner = LaunchRoutine(shipTransform, menuCanvasGroup, mainCameraTransform);
		while (true)
		{
			bool moved;
			try
			{
				moved = inner.MoveNext();
			}
			catch (System.Exception e)
			{
				Debug.LogError($"[ShipLaunchCinematic] aborted: {e}. Loading Galaxie directly.");
				SceneManager.LoadSceneAsync("Galaxie");
				yield break;
			}
			if (!moved) yield break;
			yield return inner.Current;
		}
	}

	private static IEnumerator LaunchRoutine(Transform shipTransform, CanvasGroup menuCanvasGroup, Transform m_mainCameraTransform)
	{
		if (menuCanvasGroup != null)
		{
			menuCanvasGroup.interactable = false;
			menuCanvasGroup.blocksRaycasts = false;
		}

		float elapsed = 0;
		float duration = 4.0f;

		GameObject fadeObj = new GameObject("FadeCanvas");
		Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
		fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
		fadeCanvas.sortingOrder = 999;
		fadeObj.AddComponent<GraphicRaycaster>();
		
		Image fadeImage = fadeObj.AddComponent<Image>();
		fadeImage.color = new Color(0, 0, 0, 0);

		Vector3 cameraStartPos = m_mainCameraTransform != null ? m_mainCameraTransform.position : Vector3.zero;
		Vector3 startPos = shipTransform.position;
		Quaternion startRot = shipTransform.rotation;
		
		Vector3 hoverPos = startPos + new Vector3(0, 1.5f, 0);
		Vector3 flightDir = new Vector3(0, 0.05f, -1f).normalized;

		ShipReactor[] reactors = shipTransform.GetComponentsInChildren<ShipReactor>(true);
		MaterialPropertyBlock mpb = new MaterialPropertyBlock();
		var initialScales = new Dictionary<ShipReactor, Vector3>();
		var baseLocalPos = new Dictionary<ShipReactor, Vector3>();
		var halfHeights = new Dictionary<ShipReactor, float>();

		foreach (var r in reactors)
		{
			if (r.m_renderer == null) r.m_renderer = r.GetComponentInChildren<Renderer>(true);
			if (r.m_renderer != null)
			{
				Transform t_rend = r.m_renderer.transform;
				initialScales[r] = t_rend.localScale;
				float hh = r.m_renderer.localBounds.extents.y;
				halfHeights[r] = hh;
				Vector3 base_dir = t_rend.localRotation * Vector3.down;
				baseLocalPos[r] = t_rend.localPosition + base_dir * (hh * t_rend.localScale.y);
				r.gameObject.SetActive(true);
				r.m_renderer.enabled = true;
			}
		}

		bool useFakeBoosters = (initialScales.Count == 0);
		List<Light> fakeLights = new List<Light>();
		List<TrailRenderer> fakeTrails = new List<TrailRenderer>();

		if (useFakeBoosters)
		{
			MeshFilter mf = shipTransform.GetComponent<MeshFilter>();
			if (mf == null) mf = shipTransform.GetComponentInChildren<MeshFilter>();
			Bounds b = (mf != null && mf.sharedMesh != null) ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, new Vector3(10, 5, 20));
			
			float zBack = b.min.z;
			float xSpacing = b.extents.x * 0.5f;
			float yLevel = b.center.y;
			Vector3[] engineOffsets = { new Vector3(-xSpacing, yLevel, zBack), new Vector3(xSpacing, yLevel, zBack) };

			foreach (var offset in engineOffsets)
			{
				GameObject bObj = new GameObject("FallbackEngine");
				bObj.transform.SetParent(shipTransform, false);
				bObj.transform.localPosition = offset;
				Light l = bObj.AddComponent<Light>();
				l.type = LightType.Point;
				l.color = new Color(0f, 0.7f, 1f);
				l.intensity = 0f;
				l.range = 40f;
				fakeLights.Add(l);

				TrailRenderer tr = bObj.AddComponent<TrailRenderer>();
				tr.time = 0.6f;
				tr.startWidth = 3f;
				tr.endWidth = 0f;
				// Shader.Find result is stripped in builds unless the shader is in
				// "Always Included Shaders" or referenced by a Material asset. Guard against null.
				Shader unlitShader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
				if (unlitShader != null)
				{
					Material m = new Material(unlitShader);
					m.SetColor("_Color", new Color(0f, 0.7f, 1f, 0.8f));
					tr.material = m;
				}
				tr.emitting = false;
				fakeTrails.Add(tr);
			}
		}

		float forwardOffset = 0f;

		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			float t = elapsed / duration;
			
			if (menuCanvasGroup != null) menuCanvasGroup.alpha = Mathf.Lerp(1, 0, t * 5f);

			float liftT = Mathf.Clamp01(t * 3f);
			float rotT  = Mathf.Clamp01((t - 0.1f) * 2.5f);
			float accelT= Mathf.Clamp01((t - 0.35f) * 1.5f);

			Vector3 currentPos = Vector3.Lerp(startPos, hoverPos, Mathf.SmoothStep(0, 1, liftT));
			float turnAngle = -65f * Mathf.SmoothStep(0, 1, rotT);
			shipTransform.rotation = Quaternion.AngleAxis(turnAngle, Vector3.up) * startRot;

			float currentSpeed = Mathf.Lerp(0f, 400f, accelT * accelT * accelT);
			forwardOffset += currentSpeed * Time.deltaTime;
			shipTransform.position = currentPos + (flightDir * forwardOffset);

			foreach (var r in reactors)
			{
				if (r.m_renderer != null && initialScales.ContainsKey(r))
				{
					float l_mul = Mathf.Lerp(r.m_length_range.x, r.m_length_range.y, accelT);
					float w_mul = Mathf.Lerp(r.m_width_range.x,  r.m_width_range.y,  accelT);
					Transform trnd = r.m_renderer.transform;
					float n_len = initialScales[r].y * l_mul;
					trnd.localScale = new Vector3(initialScales[r].x * w_mul, n_len, initialScales[r].z * w_mul);
					Vector3 tip_dir = trnd.localRotation * Vector3.up;
					trnd.localPosition = baseLocalPos[r] + tip_dir * (halfHeights[r] * n_len);
					
					r.m_renderer.GetPropertyBlock(mpb);
					mpb.SetColor("_CoreColor", r.m_core_color);
					mpb.SetColor("_EdgeColor", r.m_color_boost);
					mpb.SetFloat("_Brightness", Mathf.Lerp(r.m_brightness_range.x, r.m_brightness_range.y, accelT));
					mpb.SetFloat("_Alpha", Mathf.Lerp(r.m_alpha_range.x, r.m_alpha_range.y, accelT));
					mpb.SetFloat("_AnimTime", Time.time);
					r.m_renderer.SetPropertyBlock(mpb);

					if (r.m_light != null) {
						r.m_light.enabled = true;
						r.m_light.color = r.m_color_boost;
						r.m_light.intensity = Mathf.Lerp(r.m_light_intensity.x, r.m_light_intensity.y, accelT);
					}
				}
			}

			if (useFakeBoosters && accelT > 0f) {
				foreach (var l in fakeLights) l.intensity = Mathf.Lerp(0f, 150f, accelT);
				foreach (var tr in fakeTrails) tr.emitting = true;
			}

			if (m_mainCameraTransform != null)
			{
				Vector3 camTargetPos = cameraStartPos + new Vector3(6f, 4f, -2f);
				Vector3 baseCamPos = Vector3.Lerp(cameraStartPos, camTargetPos, rotT);
				if (accelT > 0f) {
					float shake = Mathf.Lerp(0, 1.5f, accelT * accelT);
					m_mainCameraTransform.position = baseCamPos + Random.insideUnitSphere * shake;
				} else
					m_mainCameraTransform.position = baseCamPos;
				m_mainCameraTransform.LookAt(shipTransform.position);
			}

			if (t > 0.7f) fadeImage.color = new Color(0, 0, 0, (t - 0.7f) / 0.3f);

			yield return null;
		}

		fadeImage.color = Color.black;
		SceneManager.LoadSceneAsync("Galaxie");
	}
}
