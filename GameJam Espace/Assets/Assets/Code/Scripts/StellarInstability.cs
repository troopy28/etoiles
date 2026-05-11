using UnityEngine;
using Assets.Code.Scripts.Generation;

public class StellarInstability : MonoBehaviour
{
	[Header("Instability Settings")]
	public float m_massFluctuationRange = 0.15f;
	public float m_cycleDurationMin = 5f;
	public float m_cycleDurationMax = 12f;
	
	[Header("Visuals")]
	public float m_pulseVisualScale = 0.04f;
	
	private SimGravityBody m_body;
	private float m_baseMass;
	private Vector3 m_baseScale;
	private Material m_material;
	private float m_timer;
	private float m_seed;
	private float m_cycleDuration;

	void Start()
	{
		m_body = GetComponent<SimGravityBody>();
		if (m_body)
			m_baseMass = m_body.mass;
		
		m_baseScale = transform.localScale;
		
		Renderer r = GetComponentInChildren<Renderer>();
		if (r) m_material = r.material;
		
		m_seed = Random.value * 100f;
		m_cycleDuration = Random.Range(m_cycleDurationMin, m_cycleDurationMax);
	}

	void Update()
	{
		if (m_body == null) return;

		m_timer += Time.deltaTime;
		float phase = Mathf.Sin((m_timer / m_cycleDuration) * Mathf.PI * 2f + m_seed);

		float targetMass = m_baseMass * (1f + phase * m_massFluctuationRange);
		m_body.mass = targetMass;
		if (m_body.m_manager != null && m_body.Id >= 0 && m_body.Id < m_body.m_manager.m_curr.Length)
		{
			var mgr = m_body.m_manager;
			int id = m_body.Id;
			var c = mgr.m_curr[id];
			mgr.m_curr[id] = new Unity.Mathematics.double4(c.x, c.y, c.z, targetMass);
			var p = mgr.m_prev[id];
			mgr.m_prev[id] = new Unity.Mathematics.double4(p.x, p.y, p.z, targetMass);
		}

		float scaleMul = 1f + phase * m_pulseVisualScale;
		transform.localScale = m_baseScale * scaleMul;

		if (m_material)
		{
			float intensity = 4.0f + phase * 2.5f;
			m_material.SetColor("_EmissionColor", m_material.color * intensity);
		}
	}
}
