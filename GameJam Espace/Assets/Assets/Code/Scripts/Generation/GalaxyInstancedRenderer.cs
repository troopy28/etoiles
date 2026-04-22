using UnityEngine;
using System.Collections.Generic;

public class GalaxyInstancedRenderer : MonoBehaviour
{
	private class InstanceBatch
	{
		public Mesh mesh;
		public Material material;
		public Color color;
		public float emission;
		public List<Matrix4x4> matrices = new List<Matrix4x4>();
	}

	private struct BatchKey : System.IEquatable<BatchKey>
	{
		public EntityId meshId;
		public EntityId materialId;
		public Color color;
		public float emission;

		public bool Equals(BatchKey other) =>
			meshId == other.meshId && materialId == other.materialId &&
			color == other.color && Mathf.Approximately(emission, other.emission);

		public override bool Equals(object obj) => obj is BatchKey k && Equals(k);
		public override int GetHashCode() => System.HashCode.Combine(meshId, materialId, color.GetHashCode(), emission);
	}
	
	private Dictionary<BatchKey, InstanceBatch> batches = new Dictionary<BatchKey, InstanceBatch>();

	private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
	private static readonly int ColorId = Shader.PropertyToID("_Color");
	private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

	private Matrix4x4[] matrixArray = new Matrix4x4[1023];
	private MaterialPropertyBlock propBlock;

	void Awake()
	{
		propBlock = new MaterialPropertyBlock();
	}

	public void ClearBatch()
	{
		batches.Clear();
	}

	public void RegisterInstance(Mesh mesh, Material material, Matrix4x4 matrix, Color color, float emission)
	{
		if (!mesh || !material)
		{
			return;
		}

		var key = new BatchKey
		{ 
			meshId = mesh.GetEntityId(), 
			materialId = material.GetEntityId(), 
			color = color, 
			emission = emission 
		};
		
		
		// <!> Cette map est recréée à chaque frame (depuis ProceduralUniverseGenerator::Update).
		if (!batches.TryGetValue(key, out InstanceBatch batch))
		{
			batch = new InstanceBatch { 
				mesh = mesh, 
				material = material, 
				color = color, 
				emission = emission 
			};
			batches[key] = batch;
		}
		batch.matrices.Add(matrix);
		
		// Debug.Log("Batch count: " + batches.Count + ", instances in batch: " + batch.matrices.Count);
	}

	private const int MAX_INSTANCES_PER_DRAW = 1023;

	void LateUpdate()
	{
		int totalDrawn = 0;
		foreach (var batch in batches.Values)
		{
			int total = batch.matrices.Count;
			if (total == 0) continue;

			if (!batch.material.enableInstancing)
				batch.material.enableInstancing = true;
			if (batch.emission > 0 && !batch.material.IsKeywordEnabled("_EMISSION"))
				batch.material.EnableKeyword("_EMISSION");

			propBlock.Clear();
			propBlock.SetColor(BaseColorId, batch.color);
			propBlock.SetColor(ColorId, batch.color);
			Color emissionColor = new Color(
				batch.color.r * batch.emission,
				batch.color.g * batch.emission,
				batch.color.b * batch.emission,
				1.0f
			);
			propBlock.SetColor(EmissionColorId, emissionColor);

			int offset = 0;
			while (offset < total)
			{
				int count = Mathf.Min(MAX_INSTANCES_PER_DRAW, total - offset);
				for (int i = 0; i < count; i++)
					matrixArray[i] = batch.matrices[offset + i];

				try
				{
					Graphics.DrawMeshInstanced(batch.mesh, 0, batch.material, matrixArray, count, propBlock);
				}
				catch (System.Exception ex)
				{
					Debug.LogWarning($"[HybridRenderer] Draw failed: {ex.Message}");
				}
				offset += count;
				totalDrawn += count;
			}
		}

		if (totalDrawn > 0 && Time.frameCount % 300 == 0)
			Debug.Log($"[HybridRenderer] Drawing {totalDrawn} instances in {batches.Count} batches.");
	}
}
