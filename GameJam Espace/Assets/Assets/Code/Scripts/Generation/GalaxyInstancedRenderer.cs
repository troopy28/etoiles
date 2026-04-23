using UnityEngine;
using System.Collections.Generic;

public class GalaxyInstancedRenderer : MonoBehaviour
{
	private class InstanceBatch
	{
		public Mesh mesh;
		public Material material;
		public List<Matrix4x4> matrices = new List<Matrix4x4>();
		public List<Vector4> colors = new List<Vector4>();
		public List<Vector4> emissions = new List<Vector4>();

		public void Clear()
		{
			matrices.Clear();
			colors.Clear();
			emissions.Clear();
		}
	}

	private struct BatchKey : System.IEquatable<BatchKey>
	{
		public EntityId meshId;
		public EntityId materialId;

		public bool Equals(BatchKey other) =>
			meshId == other.meshId && materialId == other.materialId;

		public override bool Equals(object obj) => obj is BatchKey k && Equals(k);
		public override int GetHashCode() => System.HashCode.Combine(meshId, materialId);
	}

	private Dictionary<BatchKey, InstanceBatch> batches = new Dictionary<BatchKey, InstanceBatch>();
	private List<InstanceBatch> activeBatches = new List<InstanceBatch>();

	private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
	private static readonly int ColorId = Shader.PropertyToID("_Color");
	private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

	private Matrix4x4[] matrixArray = new Matrix4x4[1023];
	private Vector4[] colorArray = new Vector4[1023];
	private Vector4[] emissionArray = new Vector4[1023];
	private MaterialPropertyBlock propBlock;

	void Awake()
	{
		propBlock = new MaterialPropertyBlock();
	}

	public void ClearBatch()
	{
		foreach (var batch in batches.Values)
			batch.Clear();
		activeBatches.Clear();
	}

	public void RegisterInstance(Mesh mesh, Material material, Matrix4x4 matrix, Color color, float emission)
	{
		if (mesh == null || material == null) return;

		var key = new BatchKey
		{ 
			meshId = mesh.GetEntityId(), 
			materialId = material.GetEntityId()
		};
		
		if (!batches.TryGetValue(key, out InstanceBatch batch))
		{
			batch = new InstanceBatch { 
				mesh = mesh, 
				material = material
			};
			batches[key] = batch;
		}

		if (batch.matrices.Count == 0)
			activeBatches.Add(batch);

		batch.matrices.Add(matrix);
		batch.colors.Add(new Vector4(color.r, color.g, color.b, color.a));
		
		Color emissionColor = new Color(
			color.r * emission,
			color.g * emission,
			color.b * emission,
			1.0f
		);
		batch.emissions.Add(new Vector4(emissionColor.r, emissionColor.g, emissionColor.b, emissionColor.a));
	}

	private const int MAX_INSTANCES_PER_DRAW = 1023;

	void LateUpdate()
	{
		int totalDrawn = 0;
		foreach (var batch in activeBatches)
		{
			int total = batch.matrices.Count;
			if (total == 0) continue;

			if (!batch.material.enableInstancing)
				batch.material.enableInstancing = true;
			
			if (!batch.material.IsKeywordEnabled("_EMISSION"))
				batch.material.EnableKeyword("_EMISSION");

			int offset = 0;
			while (offset < total)
			{
				int count = Mathf.Min(MAX_INSTANCES_PER_DRAW, total - offset);
				for (int i = 0; i < count; i++)
				{
					matrixArray[i] = batch.matrices[offset + i];
					colorArray[i] = batch.colors[offset + i];
					emissionArray[i] = batch.emissions[offset + i];
				}

				propBlock.Clear();
				propBlock.SetVectorArray(BaseColorId, colorArray);
				propBlock.SetVectorArray(ColorId, colorArray);
				propBlock.SetVectorArray(EmissionColorId, emissionArray);

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
			Debug.Log($"[HybridRenderer] Drawing {totalDrawn} instances in {activeBatches.Count} active batches (Total pooled: {batches.Count}).");
	}
}
