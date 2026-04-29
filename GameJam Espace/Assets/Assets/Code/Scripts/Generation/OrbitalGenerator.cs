using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

public class OrbitalGenerator : MonoBehaviour
{
	[Header("Prefabs")]
	public GameObject planetPrefab;
	public GameObject moonPrefab;
	public GameObject asteroidPrefab;
	public GameObject cometPrefab;
	public GameObject wreckPrefab;
	public GameObject ringPrefab;

	[Header("Materials")]
	// Shared material for planets AND moons. Both use Custom/ProceduralPlanet;
	// per-instance variation comes from PlanetVisualGenerator via MaterialPropertyBlock.
	public Material proceduralPlanetMaterial;
	public Material[] proceduralCometMaterials;

	[Header("Settings")]
	public GenerationSettings settings;
	
	[HideInInspector]
	public SimGravityManager gravityManager;

	public float GeneratePlanetarySystem(Vector3 starPos, Vector3 starVelocity, float starMass, List<StellarMath.BodyRef> allBodies, float startDistance = 80f)
	{
		int numPlanets = UnityEngine.Random.Range(settings.planetCountRange.x, settings.planetCountRange.y + 1);
		float currentDistance = startDistance;
		float gmPhysical = gravityManager.G * (Mathf.Abs(starMass) * settings.gameGravityMassMultiplier);

		for (int p = 0; p < numPlanets; p++)
		{
			currentDistance += UnityEngine.Random.Range(settings.planetSpacingRange.x, settings.planetSpacingRange.y) * settings.distanceScale * settings.systemSpread;

			float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
			Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * currentDistance;
			
			offset.y = UnityEngine.Random.Range(-5f, 5f);
			Vector3 planetPos = starPos + offset;

			float r = offset.magnitude;
			float orbitalSpeed = Mathf.Sqrt(gmPhysical / r);
			
			Vector3 orbitTangent = Vector3.Cross(Vector3.up, offset).normalized;
			Vector3 planetVelocity = starVelocity + (orbitTangent * orbitalSpeed);

			float planetMass = UnityEngine.Random.Range(0.001f, 0.05f);
			
            if (!planetPrefab) continue;

			GameObject planetObj = Instantiate(planetPrefab, planetPos, Quaternion.identity, this.transform);
			planetObj.name = $"Planet_{p}";
			
			float baseScale = UnityEngine.Random.Range(0.5f, 2f);
			float finalScale = baseScale * (settings.visualScaleMultiplier * 0.2f) * settings.bodySizeScale;
			planetObj.transform.localScale = new Vector3(finalScale, finalScale, finalScale);
			
			int planetSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
			Color color = PlanetVisualGenerator.Apply(planetObj, planetSeed, proceduralPlanetMaterial, isMoon: false);

			SimGravityBody body = planetObj.GetComponent<SimGravityBody>();
			if (body)
			{
				body.m_manager = gravityManager;
				body.mass = planetMass * settings.gameGravityMassMultiplier;
				body.m_initial_velocity = new float3(planetVelocity.x, planetVelocity.y, planetVelocity.z);
				body.kind = BodyKind.Planet;
                
                Renderer rComp = planetObj.GetComponentInChildren<Renderer>();
                MeshFilter mf = planetObj.GetComponentInChildren<MeshFilter>();
				allBodies.Add(new StellarMath.BodyRef { 
                    body = body,
                    bodyID = -1,
                    renderer = rComp,
                    mesh = mf ? mf.sharedMesh : null,
                    material = rComp ? rComp.sharedMaterial : null,
                    color = color,
                    emission = 0f
                });
			}

			if (ringPrefab && planetMass > 0.03f && UnityEngine.Random.value < settings.ringChance)
			{
				GameObject ringObj = Instantiate(ringPrefab, planetPos, Quaternion.identity, planetObj.transform);
				ringObj.transform.localScale = Vector3.one * 2.5f;
				ringObj.transform.localRotation = Quaternion.Euler(UnityEngine.Random.Range(10, 30), 0, 0);
			}

			if (moonPrefab && UnityEngine.Random.value < settings.moonChance)
				GenerateMoons(planetPos, planetVelocity, planetMass, finalScale, allBodies);
		}

		return currentDistance;
	}

	public void GenerateMoons(Vector3 planetPos, Vector3 planetVelocity, float planetMass, float planetVisualScale, List<StellarMath.BodyRef> allBodies)
	{
        if (!moonPrefab) return;

		int numMoons = UnityEngine.Random.Range(1, 4);
		float currentDistance = Mathf.Max(planetVisualScale * 3f, 5f);
		float gmPhysical = gravityManager.G * (planetMass * settings.gameGravityMassMultiplier);

		for(int m = 0; m < numMoons; m++)
		{
			currentDistance += UnityEngine.Random.Range(planetVisualScale * 0.3f, planetVisualScale * 1f);
			
			float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
			Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * currentDistance;
			offset.y = UnityEngine.Random.Range(-2f, 2f);
			
			Vector3 moonPos = planetPos + offset;
			float r = offset.magnitude;
			float orbitalSpeed = Mathf.Sqrt(gmPhysical / r);
			Vector3 orbitTangent = Vector3.Cross(Vector3.up, offset).normalized;
			Vector3 moonVelocity = planetVelocity + (orbitTangent * orbitalSpeed);

			float moonMass = planetMass * UnityEngine.Random.Range(0.01f, 0.1f);

			GameObject moonObj = Instantiate(moonPrefab, moonPos, Quaternion.identity, this.transform);
			moonObj.name = $"Moon_{m}";

			float moonScale = UnityEngine.Random.Range(0.2f, 0.5f) * planetVisualScale;
			moonObj.transform.localScale = new Vector3(moonScale, moonScale, moonScale);

			int moonSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
			Color color = PlanetVisualGenerator.Apply(moonObj, moonSeed, proceduralPlanetMaterial, isMoon: true);

			SimGravityBody body = moonObj.GetComponent<SimGravityBody>();
			if (body)
			{
				body.m_manager = gravityManager;
				body.mass = moonMass * settings.gameGravityMassMultiplier;
				body.m_initial_velocity = new float3(moonVelocity.x, moonVelocity.y, moonVelocity.z);
				body.kind = BodyKind.Moon;
                
                Renderer rComp = moonObj.GetComponentInChildren<Renderer>();
                MeshFilter mf = moonObj.GetComponentInChildren<MeshFilter>();
				allBodies.Add(new StellarMath.BodyRef { 
                    body = body, 
                    bodyID = -1,
                    renderer = rComp,
                    mesh = mf ? mf.sharedMesh : null,
                    material = rComp? rComp.sharedMaterial : null,
                    color = color,
                    emission = 0f
                });
			}
		}
	}

	public void GenerateAsteroidBelt(Vector3 starPos, Vector3 starVelocity, float starMass, float distance, List<StellarMath.BodyRef> allBodies)
	{
		int numAsteroids = UnityEngine.Random.Range(30, 80);
		float gmPhysical = gravityManager.G * (Mathf.Abs(starMass) * settings.gameGravityMassMultiplier);

		for(int i = 0; i < numAsteroids; i++)
		{
			float distVariation = distance + UnityEngine.Random.Range(-settings.asteroidBeltVariance, settings.asteroidBeltVariance) * settings.distanceScale;
			float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

			Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distVariation;
			offset.y = UnityEngine.Random.Range(-3f, 3f);

			Vector3 asteroidPos = starPos + offset;
			float r = offset.magnitude;
			float orbitalSpeed = Mathf.Sqrt(gmPhysical / r);
			Vector3 orbitTangent = Vector3.Cross(Vector3.up, offset).normalized;
			Vector3 asteroidVelocity = starVelocity + (orbitTangent * orbitalSpeed);

			if (wreckPrefab && UnityEngine.Random.value < 0.05f)
			{
				GenerateWreck(asteroidPos, asteroidVelocity, allBodies);
				continue;
			}

            if (!asteroidPrefab) continue;

			GameObject asteroidObj = Instantiate(asteroidPrefab, asteroidPos, Quaternion.identity, this.transform);
			asteroidObj.name = $"Asteroid_{i}";

			float astScale = UnityEngine.Random.Range(0.5f, 3f) * settings.bodySizeScale;
			asteroidObj.transform.localScale = new Vector3(astScale, astScale, astScale);
			
			Color color = Color.HSVToRGB(Mathf.Lerp(0.05f, 0.15f, UnityEngine.Random.value), UnityEngine.Random.Range(0f, 0.3f), UnityEngine.Random.Range(0.2f, 0.5f));
			StellarMath.MatchMaterialColor(asteroidObj, color, 0f);

			SimGravityBody body = asteroidObj.GetComponent<SimGravityBody>();
			if (body)
			{
				body.m_manager = gravityManager;
				body.mass = 0.0001f * settings.gameGravityMassMultiplier;
				body.m_initial_velocity = new float3(asteroidVelocity.x, asteroidVelocity.y, asteroidVelocity.z);
				body.kind = BodyKind.Asteroid;
				
                Renderer rComp = asteroidObj.GetComponentInChildren<Renderer>();
                MeshFilter mf = asteroidObj.GetComponentInChildren<MeshFilter>();
				allBodies.Add(new StellarMath.BodyRef { 
                    body = body, 
                    bodyID = -1,
                    renderer = rComp,
                    mesh = mf ? mf.sharedMesh : null,
                    material = rComp ? rComp.sharedMaterial : null,
                    color = color,
                    emission = 0f
                });
			}
		}
	}

	public void GenerateComet(Vector3 starPos, Vector3 starVelocity, float starMass, List<StellarMath.BodyRef> allBodies)
	{
        if (!cometPrefab) return;

		float distance = UnityEngine.Random.Range(settings.cometDistanceRangeMin, settings.cometDistanceRangeMax) * settings.distanceScale * settings.systemSpread;
		float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
		Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
		
		Vector3 cometPos = starPos + offset;
		float gmPhysical = gravityManager.G * (Mathf.Abs(starMass) * settings.gameGravityMassMultiplier);
		
		float orbitalSpeed = Mathf.Sqrt(gmPhysical / distance) * settings.cometSpeedMultiplier; 
		
		Vector3 orbitTangent = Vector3.Cross(Vector3.up, offset).normalized;
		Vector3 cometVelocity = starVelocity + (orbitTangent * orbitalSpeed);

		GameObject cometObj = Instantiate(cometPrefab, cometPos, Quaternion.identity, this.transform);
		cometObj.name = "Comet_WaterSource";

		float scale = UnityEngine.Random.Range(2f, 5f) * settings.bodySizeScale;
		cometObj.transform.localScale = new Vector3(scale, scale, scale);

        Color color = Color.cyan;
		if (proceduralCometMaterials != null && proceduralCometMaterials.Length > 0)
		{
			Material chosenMat = proceduralCometMaterials[UnityEngine.Random.Range(0, proceduralCometMaterials.Length)];
			Renderer ren = cometObj.GetComponentInChildren<Renderer>();
			if (ren) ren.sharedMaterial = chosenMat;
		}

		SimGravityBody body = cometObj.GetComponent<SimGravityBody>();
		if (body)
		{
			body.m_manager = gravityManager;
			body.mass = 0.001f * settings.gameGravityMassMultiplier;
			body.m_initial_velocity = new float3(cometVelocity.x, cometVelocity.y, cometVelocity.z);
			body.kind = BodyKind.Comet;
			
            Renderer rComp = cometObj.GetComponentInChildren<Renderer>();
            MeshFilter mf = cometObj.GetComponentInChildren<MeshFilter>();
			allBodies.Add(new StellarMath.BodyRef { 
                body = body, 
                bodyID = -1,
                renderer = rComp,
                mesh = mf ? mf.sharedMesh : null,
                material = rComp? rComp.sharedMaterial : null,
                color = color,
                emission = 1f
            });
		}
	}

	public void GenerateWreck(Vector3 pos, Vector3 vel, List<StellarMath.BodyRef> allBodies)
	{
        if (!wreckPrefab) return;

		GameObject wreckObj = Instantiate(wreckPrefab, pos, Quaternion.identity, this.transform);
		wreckObj.name = "Wreck_PartSource";
		
		wreckObj.transform.localScale = Vector3.one * 5f * settings.bodySizeScale;

		SimGravityBody body = wreckObj.GetComponent<SimGravityBody>();
		if (body)
		{
			body.m_manager = gravityManager;
			body.mass = 0.0001f;
			body.m_initial_velocity = new float3(vel.x, vel.y, vel.z);
			body.kind = BodyKind.Wreck;
			
            Renderer rComp = wreckObj.GetComponentInChildren<Renderer>();
            MeshFilter mf = wreckObj.GetComponentInChildren<MeshFilter>();
			allBodies.Add(new StellarMath.BodyRef { 
                body = body, 
                bodyID = -1,
                renderer = rComp,
                mesh = mf ? mf.sharedMesh : null,
                material = rComp ? rComp.sharedMaterial : null,
                color = Color.gray,
                emission = 0f
            });
		}
	}
}
