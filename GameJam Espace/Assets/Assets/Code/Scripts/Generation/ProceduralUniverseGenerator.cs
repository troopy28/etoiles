using UnityEngine;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

public class ProceduralUniverseGenerator : MonoBehaviour
{
	public enum GenerationShape { Sphere, Disc, Spiral, RaceCorridor }

	[Header("Prefabs")]
	public GameObject starPrefab;
	public GameObject startPointPrefab;
	public GameObject endPointPrefab;
	public Transform playerTransform;

	[Header("Optimization (LOD)")]
	public float simulationDistance = 2500f;
	public float lodUpdateInterval = 0.5f;

	[Header("Procedural Materials")]
	public Material[] proceduralStarMaterials;

	[Header("Settings & Layout")]
	public GenerationSettings settings;
	public GenerationShape universeShape = GenerationShape.RaceCorridor;

	[Header("References")]
	public SimGravityManager gravityManager;
	public OrbitalGenerator orbitalGenerator;
	public GalaxyInstancedRenderer instancedRenderer;

	public bool generateOnStart = true;
	private bool isGenerating = false;
	private float nextLodUpdateTime = 0f;

	private List<StellarMath.BodyRef> bodyRefs = new List<StellarMath.BodyRef>();
	private NativeArray<bool> visibilityResults;
	private NativeArray<int> bodyIDs;
	private bool resultsCreated = false;

	private Camera mainCam;
	private float simulationDistanceSq;

	private static FieldInfo _bodyIdField;
	private int GetBodyID(SimGravityBody body)
	{
		if (body == null) return -1;
		if (_bodyIdField == null)
			_bodyIdField = typeof(SimGravityBody).GetField("m_id", BindingFlags.NonPublic | BindingFlags.Instance);
		return (int)_bodyIdField.GetValue(body);
	}

	void Start()
	{
		if (gravityManager == null) gravityManager = FindAnyObjectByType<SimGravityManager>();
		if (orbitalGenerator == null) orbitalGenerator = GetComponent<OrbitalGenerator>();
		
		if (orbitalGenerator != null) 
		{
			orbitalGenerator.settings = settings;
			orbitalGenerator.gravityManager = gravityManager;
		}

		if (instancedRenderer == null) instancedRenderer = GetComponent<GalaxyInstancedRenderer>();

		if (settings != null && settings.seed != 0)
			UnityEngine.Random.InitState(settings.seed);

		mainCam = Camera.main;
		simulationDistanceSq = simulationDistance * simulationDistance;

		if (generateOnStart)
			GenerateUniverse();
	}

	void OnDestroy()
	{
		if (resultsCreated) {
			visibilityResults.Dispose();
			bodyIDs.Dispose();
		}
	}

	void Update()
	{
		if (isGenerating) return;

		if (Time.time > nextLodUpdateTime)
		{
			nextLodUpdateTime = Time.time + lodUpdateInterval;
			UpdateCulling();
		}

		if (instancedRenderer != null && bodyRefs.Count > 0)
		{
			instancedRenderer.ClearBatch();
			if (mainCam == null) mainCam = Camera.main;
			if (mainCam == null) return;

			Vector3 camPos = mainCam.transform.position;

			for (int i = 0; i < bodyRefs.Count; i++)
			{
				var r = bodyRefs[i];
				if (r.body == null || r.mesh == null) continue;

				if (r.bodyID == -1)
				{
					int idValue = GetBodyID(r.body);
					if (idValue >= 0) 
					{
						if (gravityManager.m_curr.Length > idValue)
						{
							r.bodyID = idValue;
							bodyRefs[i] = r;
						}
					}
				}

				int bid = r.bodyID;
				if (bid < 0 || bid >= gravityManager.m_curr.Length) continue;

				float3 pos = gravityManager.m_curr[bid].xyz;
				float distSq = math.distancesq(pos, (float3)camPos);
				
				if (distSq > simulationDistanceSq)
				{
					Matrix4x4 mat = Matrix4x4.TRS((Vector3)pos, r.body.transform.rotation, r.body.transform.localScale);
					instancedRenderer.RegisterInstance(r.mesh, r.material, mat, r.color, r.emission);
				}
			}
		}
	}

	private void UpdateCulling()
	{
		if (gravityManager == null || bodyRefs.Count == 0) return;
		if (mainCam == null) mainCam = Camera.main;
		if (mainCam == null) return;

		Vector3 camPos = mainCam.transform.position;
		int count = bodyRefs.Count;

		if (!resultsCreated || visibilityResults.Length != count)
		{
			if (resultsCreated) {
				visibilityResults.Dispose();
				bodyIDs.Dispose();
			}
			visibilityResults = new NativeArray<bool>(count, Allocator.Persistent);
			bodyIDs = new NativeArray<int>(count, Allocator.Persistent);
			resultsCreated = true;
		}

		for(int i = 0; i < count; i++) {
			var r = bodyRefs[i];
			if (r.bodyID == -1 && r.body != null) {
				int idVal = GetBodyID(r.body);
				if (idVal >= 0) {
					 if (gravityManager.m_curr.Length > idVal) {
						r.bodyID = idVal;
						bodyRefs[i] = r;
					 }
				}
			}
			bodyIDs[i] = Mathf.Max(0, r.bodyID);
		}

		var job = new LODVisibilityJob
		{
			positions = gravityManager.m_curr.AsArray(),
			bodyIDs = bodyIDs,
			cameraPos = camPos,
			simulationDistanceSq = simulationDistanceSq,
			visibilityResults = visibilityResults
		};

		JobHandle handle = job.Schedule(count, 64);
		handle.Complete();

		for (int i = 0; i < count; i++)
		{
			var r = bodyRefs[i];
			bool visible = visibilityResults[i];
			if (r.bodyID == -1 || r.mesh == null) visible = true;

			if (r.body != null)
			{
				if (!r.body.enabled) r.body.enabled = true;
				if (r.renderer != null && r.renderer.enabled != visible) r.renderer.enabled = visible;
			}
		}
	}

	public void GenerateUniverse()
	{
		if (isGenerating) return;
		if (starPrefab == null || gravityManager == null || settings == null) return;

		foreach (Transform child in transform)
			if (child != null) Destroy(child.gameObject);

		bodyRefs.Clear();
		StartCoroutine(GenerateUniverseRoutine());
	}

	private IEnumerator GenerateUniverseRoutine()
	{
		isGenerating = true;
		List<Vector3> generatedStarPositions = new List<Vector3>();
		int systemsPlaced = 0;

		Debug.Log($"[Generator] Mission Started: Generating trade route A -> B ({settings.starSystemCount} systems)");

		for (int i = 0; i < settings.starSystemCount; i++)
		{
			Vector3 sysPos = GetValidSystemPosition(i, generatedStarPositions);
			if (sysPos.x == float.PositiveInfinity) continue;

			try {
				generatedStarPositions.Add(sysPos);
				systemsPlaced++;

				bool isBinary = UnityEngine.Random.value < settings.binaryStarChance;
				float systemMass = 0f;
				StellarClass sc = StellarClass.G;
				Vector3 sysDrift = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(0f, settings.maxSystemDriftVelocity);

				if (i == 0) { isBinary = false; sc = StellarClass.G; sysDrift = Vector3.zero; }
				if (i == settings.starSystemCount - 1) { isBinary = false; sc = StellarClass.A; sysDrift = Vector3.zero; }

				if (isBinary) {
					GenerateBinaryStar(sysPos, sysDrift, out systemMass);
					sc = StellarClass.A; 
				}
				else
					GenerateStar(sysPos, sysDrift, out systemMass, out sc, i == 0, i == settings.starSystemCount -1);

				if (i == 0 && playerTransform != null)
				{
					playerTransform.position = sysPos + new Vector3(0, 50, -200);
					playerTransform.LookAt(sysPos);
					Debug.Log("[Generator] Player teleported to Starting Point A.");
				}

				if (orbitalGenerator != null) {
					float resourceChanceMultiplier = (universeShape == GenerationShape.RaceCorridor) ? 1.5f : 1.0f;
					
					if (UnityEngine.Random.value < settings.cometChance * resourceChanceMultiplier)
						orbitalGenerator.GenerateComet(sysPos, sysDrift, systemMass, bodyRefs);

					float startDist = isBinary ? settings.binarySystemStartDist : settings.singleSystemStartDist;
					float outmost = orbitalGenerator.GeneratePlanetarySystem(sysPos, sysDrift, systemMass, bodyRefs, startDist);

					if (UnityEngine.Random.value < settings.asteroidBeltChance * resourceChanceMultiplier) {
						float beltDist = outmost + UnityEngine.Random.Range(50f, 150f);
						orbitalGenerator.GenerateAsteroidBelt(sysPos, sysDrift, systemMass, beltDist, bodyRefs);
					}

					if (i < settings.starSystemCount - 1)
						SpawnGapFillers(sysPos, i, systemsPlaced);
				}
			} catch (System.Exception e) {
				Debug.LogError($"[Generator] Error at system {i}: {e.Message}");
			}

			yield return null;
		}

		isGenerating = false;
		Debug.Log($"[Generator] Mission Layout Ready. {systemsPlaced} systems on path.");
	}

	private void SpawnGapFillers(Vector3 currentPos, int index, int count)
	{
		int fillers = UnityEngine.Random.Range(1, 4);

		float nextProgress = (float)(index + 1) / (float)settings.starSystemCount;
		float nextZProg = (nextProgress * 2f - 1f) * settings.corridorLength;
		float nextWindX = Mathf.Sin(nextZProg * 0.001f) * (settings.corridorRadius * 0.5f);
		float nextWindY = Mathf.Cos(nextZProg * 0.0015f) * (settings.corridorRadius * 0.5f);
		Vector3 estimatedNext = transform.position + new Vector3(nextWindX, nextWindY, nextZProg);
		Vector3 dirToNext = (estimatedNext - currentPos).normalized;
		float gapDistance = Vector3.Distance(currentPos, estimatedNext);

		for (int f = 0; f < fillers; f++)
		{
			float step = (float)(f + 1) / (fillers + 1);
			Vector3 fillerPos = currentPos + dirToNext * (gapDistance * step)
				+ new Vector3(
					UnityEngine.Random.Range(-settings.corridorRadius * 0.4f, settings.corridorRadius * 0.4f),
					UnityEngine.Random.Range(-settings.corridorRadius * 0.4f, settings.corridorRadius * 0.4f),
					0f
				);

			if (UnityEngine.Random.value < 0.3f)
				orbitalGenerator.GenerateWreck(fillerPos, Vector3.zero, bodyRefs);
			else if (UnityEngine.Random.value < 0.1f)
				orbitalGenerator.GenerateComet(fillerPos, Vector3.zero, 0f, bodyRefs);
		}
	}

	private Vector3 GetValidSystemPosition(int index, List<Vector3> existingPositions)
	{
		Vector3 pos = Vector3.zero;
		int attempts = 0;
		float targetSafetyRadius = settings.minStarDistance * 1.1f;

		while (attempts < 100)
		{
			bool valid = true;
			pos = transform.position;
			float progress = (float)attempts / 100f;
			float currentSafety = targetSafetyRadius * Mathf.Max(0.1f, 1f - progress);
			
			switch (universeShape)
			{
				case GenerationShape.Sphere: pos += UnityEngine.Random.insideUnitSphere * settings.corridorLength; break;
				case GenerationShape.Disc: 
					Vector2 c = UnityEngine.Random.insideUnitCircle * settings.corridorLength; 
					pos += new Vector3(c.x, UnityEngine.Random.Range(-50f, 50f), c.y); 
					break;
				case GenerationShape.Spiral:
					float angle = UnityEngine.Random.Range(0f, Mathf.PI * 4f);
					float dist = UnityEngine.Random.Range(0.1f, 1f) * settings.corridorLength;
					int arms = 3;
					angle += (index % arms) * (Mathf.PI * 2f / arms);
					angle += dist / settings.corridorLength * 5f;
					pos += new Vector3(Mathf.Cos(angle) * dist, UnityEngine.Random.Range(-50f, 50f), Mathf.Sin(angle) * dist);
					break;
				case GenerationShape.RaceCorridor:
					float zNormalized = ((float)index / (float)settings.starSystemCount) * 2f - 1f;
					float zProg = zNormalized * settings.corridorLength;
					
					zProg += UnityEngine.Random.Range(-settings.minStarDistance, settings.minStarDistance);

					Vector2 tube = UnityEngine.Random.insideUnitCircle * settings.corridorRadius;
					float windX = Mathf.Sin(zProg * 0.001f) * (settings.corridorRadius * 0.5f);
					float windY = Mathf.Cos(zProg * 0.0015f) * (settings.corridorRadius * 0.5f);
					pos += new Vector3(tube.x + windX, tube.y + windY, zProg);
					break;
			}

			foreach (Vector3 other in existingPositions)
			{
				if (Vector3.Distance(pos, other) < currentSafety) {
					valid = false;
					break;
				}
			}
			
			if (valid) return pos;
			attempts++;
		}
		
		return new Vector3(float.PositiveInfinity, 0, 0);
	}

	private void GenerateStar(Vector3 position, Vector3 sysVel, out float mass, out StellarClass sc, bool isA = false, bool isB = false)
	{
		StarProperties props = StellarMath.GetRandomStarProperties();
		mass = props.mass;
		sc = props.sc;

		GameObject instance;
		if (isA && startPointPrefab != null) instance = Instantiate(startPointPrefab, position, Quaternion.identity, transform);
		else if (isB && endPointPrefab != null) instance = Instantiate(endPointPrefab, position, Quaternion.identity, transform);
		else instance = Instantiate(starPrefab, position, Quaternion.identity, transform);

		instance.name = isA ? "POINT_A_START" : (isB ? "POINT_B_END" : $"Star_{props.sc}");
		float rad = Mathf.Max(1.0f, props.radius * settings.visualScaleMultiplier);
		if (isA || isB) rad *= 2.0f;
		instance.transform.localScale = new Vector3(rad, rad, rad);

		if (proceduralStarMaterials != null && proceduralStarMaterials.Length > 0 && props.sc != StellarClass.Remnant_BlackHole)
		{
			Material chosenMat = proceduralStarMaterials[UnityEngine.Random.Range(0, proceduralStarMaterials.Length)];
			Renderer r = instance.GetComponentInChildren<Renderer>();
			if (r != null) r.sharedMaterial = chosenMat;
		}

		SimGravityBody body = instance.GetComponent<SimGravityBody>();
		if (body != null)
		{
			body.m_manager = gravityManager;
			body.mass = props.mass * settings.gameGravityMassMultiplier;
			body.m_initial_velocity = new float3(sysVel.x, sysVel.y, sysVel.z);
			
			Renderer rComp = instance.GetComponentInChildren<Renderer>();
			MeshFilter mf = instance.GetComponentInChildren<MeshFilter>();
			if (mf == null) mf = instance.GetComponent<MeshFilter>();

			bodyRefs.Add(new StellarMath.BodyRef { 
				body = body,
				bodyID = -1,
				renderer = rComp,
				mesh = mf ? mf.sharedMesh : null,
				material = rComp ? rComp.sharedMaterial : null,
				color = props.color,
				emission = (props.sc == StellarClass.Remnant_BlackHole) ? 0f : (props.sc == StellarClass.Remnant_WhiteHole ? 12.0f : 4.0f)
			});
		}
		StellarMath.MatchMaterialColor(instance, props.color, 4.0f);
	}

	private void GenerateBinaryStar(Vector3 centerPos, Vector3 systemVelocity, out float totalMass)
	{
		float m1 = UnityEngine.Random.Range(0.5f, 5.0f);
		float m2 = m1 * UnityEngine.Random.Range(0.5f, 1.0f);
		totalMass = m1 + m2;
		float sep = UnityEngine.Random.Range(15f, 40f);
		float gm = gravityManager.G * (totalMass * settings.gameGravityMassMultiplier);
		float v = Mathf.Sqrt(gm / sep);

		Vector3 axis = UnityEngine.Random.onUnitSphere;
		Vector3 offset = Vector3.Cross(axis, Vector3.up).normalized * (sep * 0.5f);
		Vector3 tangent = Vector3.Cross(offset, axis).normalized;

		Vector3 pos1 = centerPos - offset;
		Vector3 vel1 = systemVelocity - tangent * (v * (m2 / totalMass));
		SpawnSingleStar(pos1, vel1, m1);

		Vector3 pos2 = centerPos + offset;
		Vector3 vel2 = systemVelocity + tangent * (v * (m1 / totalMass));
		SpawnSingleStar(pos2, vel2, m2);
	}

	private void SpawnSingleStar(Vector3 pos, Vector3 vel, float mass)
	{
		StarProperties props = StellarMath.GetRandomStarProperties(mass);
		GameObject starInstance = Instantiate(starPrefab, pos, Quaternion.identity, this.transform);
		starInstance.name = $"Star_BinaryPart";
		float rad = Mathf.Max(1.0f, props.radius * settings.visualScaleMultiplier);
		starInstance.transform.localScale = new Vector3(rad, rad, rad);

		if (proceduralStarMaterials != null && proceduralStarMaterials.Length > 0)
		{
			Material chosenMat = proceduralStarMaterials[UnityEngine.Random.Range(0, proceduralStarMaterials.Length)];
			Renderer r = starInstance.GetComponentInChildren<Renderer>();
			if (r != null) r.sharedMaterial = chosenMat;
		}

		SimGravityBody body = starInstance.GetComponent<SimGravityBody>();
		if (body != null)
		{
			body.m_manager = gravityManager;
			body.mass = props.mass * settings.gameGravityMassMultiplier;
			body.m_initial_velocity = new float3(vel.x, vel.y, vel.z);
			
			Renderer rComp = starInstance.GetComponentInChildren<Renderer>();
			MeshFilter mf = starInstance.GetComponentInChildren<MeshFilter>();
			if (mf == null) mf = starInstance.GetComponent<MeshFilter>();

			bodyRefs.Add(new StellarMath.BodyRef { 
				body = body, 
				bodyID = -1,
				renderer = rComp,
				mesh = mf ? mf.sharedMesh : null,
				material = rComp ? rComp.sharedMaterial : null,
				color = props.color,
				emission = 4.0f
			});
		}
		StellarMath.MatchMaterialColor(starInstance, props.color, 4.0f);
	}
}
