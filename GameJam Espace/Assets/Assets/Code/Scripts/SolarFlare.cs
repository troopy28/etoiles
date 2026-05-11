using UnityEngine;
using System.Collections;
using Unity.Mathematics;

public class SolarFlare : MonoBehaviour
{
	[Header("Flare Settings")]
	public float m_flareIntervalMin = 18f;
	public float m_flareIntervalMax = 45f;
	public float m_flareDuration = 5f;
	public float m_flareMaxForce = 50f;
	
	private SimGravityBody m_body;
	private ShipControl m_player;
	private float m_currentFlarePower = 0f;
	private float3 m_currentPushForce = float3.zero;
	private Material m_material;
	private Color m_baseEmissionColor;

	void Start()
	{
		m_body = GetComponent<SimGravityBody>();
		m_player = FindAnyObjectByType<ShipControl>();
		
		Renderer r = GetComponentInChildren<Renderer>();
		if (r)
		{
			m_material = r.material;
			m_baseEmissionColor = m_material.GetColor("_EmissionColor");
		}
		
		StartCoroutine(FlareRoutine());
	}

	IEnumerator FlareRoutine()
	{
		yield return new WaitForSeconds(UnityEngine.Random.Range(5f, 20f));
		
		while (true)
		{
			yield return new WaitForSeconds(UnityEngine.Random.Range(m_flareIntervalMin, m_flareIntervalMax));
			yield return TriggerFlare();
		}
	}

	IEnumerator TriggerFlare()
	{
		float elapsed = 0;
		while (elapsed < m_flareDuration)
		{
			elapsed += Time.deltaTime;
			
			m_currentFlarePower = Mathf.Sin((elapsed / m_flareDuration) * Mathf.PI);
			
			if (m_material)
				m_material.SetColor("_EmissionColor", m_baseEmissionColor * (1f + m_currentFlarePower * 3f));

			ApplyFlareEffect();
			
			yield return null;
		}
		
		m_currentPushForce = float3.zero;
		if (m_material) m_material.SetColor("_EmissionColor", m_baseEmissionColor);
		m_currentFlarePower = 0f;
		
		// Remettre l'accélération externe à zéro une fois la tempête terminée
		if (m_player && m_player.m_gravity_body != null && m_player.m_gravity_body.m_manager != null)
		{
			int id = m_player.m_gravity_body.Id;
			m_player.m_gravity_body.m_manager.SetExternalAcceleration(id, float3.zero);
		}
	}

	void FixedUpdate()
	{
		if (m_player && m_player.m_gravity_body != null && m_player.m_gravity_body.m_manager != null
			&& math.lengthsq(m_currentPushForce) > 0)
		{
			var mgr = m_player.m_gravity_body.m_manager;
			int id = m_player.m_gravity_body.Id;
			// Ajouter la poussée de la tempête par-dessus la poussée du vaisseau
			// (ThrustAccelWorld est déjà soumis au sim par ShipControl.FixedUpdate ;
			// on l'additionne ici pour que SetExternalAcceleration reflète les deux)
			Vector3 totalAcc = m_player.ThrustAccelWorld + (Vector3)(float3)m_currentPushForce;
			mgr.SetExternalAcceleration(id, (float3)totalAcc);
		}
	}

	void ApplyFlareEffect()
	{
		if (m_player == null) m_player = FindAnyObjectByType<ShipControl>();
		if (m_player == null) return;
		
		float dist = Vector3.Distance(transform.position, m_player.transform.position);
		float starRadius = transform.localScale.x * 0.5f;
		float effectRadius = starRadius * 8f;
		
		if (dist < effectRadius)
		{
			float falloff = 1f - Mathf.Clamp01(dist / effectRadius);
			float totalPower = falloff * m_currentFlarePower;
			
			Vector3 pushDir = (m_player.transform.position - transform.position).normalized;
			m_currentPushForce = (float3)(pushDir * m_flareMaxForce * totalPower);

			m_player.AddEnvironmentalHeat(totalPower * 80f);
		}
	}
}
