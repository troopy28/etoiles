using UnityEngine;

public class CargoManager : MonoBehaviour
{
	public static CargoManager Instance;

	[Header("Cargo Settings")]
	public string m_cargoName = "Marchandises Vitales";
	public float m_maxIntegrity = 100f;
	public float m_currentIntegrity = 100f;
	public float m_cargoMass = 50f;
	
	[Header("Damage Thresholds")]
	public float m_gForceThreshold = 40f;
	public float m_gForceDamageMultiplier = 0.5f;
	public float m_heatDamageThreshold = 80f;
	public float m_heatDamageMultiplier = 2.0f;

	private ShipControl m_ship;
	private Vector3 m_lastVelocity;
	private float m_originalShipMass;
	private bool m_initialized = false;

	void Awake()
	{
		Instance = this;
	}

	void Start()
	{
		m_ship = GetComponent<ShipControl>();
	}

	void Update()
	{
		if (!m_ship) return;

		if (!m_initialized && m_ship.m_gravity_body && m_ship.m_gravity_body.Id >= 0)
		{
			m_originalShipMass = m_ship.m_gravity_body.mass;
			UpdateShipMass();
			m_lastVelocity = m_ship.VelocityWorld;
			m_initialized = true;
		}

		if (!m_initialized) return;

		float dt = Time.deltaTime;
		if (dt <= 0) return;

		Vector3 currentVelocity = m_ship.VelocityWorld;
		Vector3 acceleration = (currentVelocity - m_lastVelocity) / dt;
		float gForce = acceleration.magnitude / 9.81f;
		
		if (gForce > m_gForceThreshold)
		{
			float damage = (gForce - m_gForceThreshold) * m_gForceDamageMultiplier * dt;
			ApplyDamage(damage, "contraintes mécaniques (G élevés)");
		}
		m_lastVelocity = currentVelocity;

		float tempRatio = m_ship.TemperatureRatio * 100f;
		if (tempRatio > m_heatDamageThreshold)
		{
			float damage = (tempRatio - m_heatDamageThreshold) * m_heatDamageMultiplier * dt;
			ApplyDamage(damage, "dégradation thermique");
		}
	}

	public void ApplyDamage(float amount, string reason)
	{
		if (amount <= 0) return;
		m_currentIntegrity -= amount;
		if (m_currentIntegrity <= 0)
		{
			m_currentIntegrity = 0;
			if (GameProgressManager.Instance)
				GameProgressManager.Instance.Die("Cargo Destruction (" + reason + ")");
		}
	}

	private void UpdateShipMass()
	{
		if (m_ship && m_ship.m_gravity_body)
		{
			float totalMass = m_originalShipMass + m_cargoMass;
			m_ship.m_gravity_body.mass = totalMass;
			
			var mgr = m_ship.m_gravity_body.m_manager;
			int id = m_ship.m_gravity_body.Id;
			if (mgr != null && id >= 0 && id < mgr.m_curr.Length)
			{
				// Mettre à jour la masse dans les buffers Verlet (w = mass dans double4)
				var c = mgr.m_curr[id];
				mgr.m_curr[id] = new Unity.Mathematics.double4(c.x, c.y, c.z, totalMass);
				var p = mgr.m_prev[id];
				mgr.m_prev[id] = new Unity.Mathematics.double4(p.x, p.y, p.z, totalMass);
				Debug.Log($"[CargoManager] Ship mass updated: {m_originalShipMass} -> {totalMass} (Cargo: {m_cargoMass})");
			}
		}
	}
}
