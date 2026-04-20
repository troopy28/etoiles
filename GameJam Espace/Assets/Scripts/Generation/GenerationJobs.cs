using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct LODVisibilityJob : IJobParallelFor
{
	[ReadOnly] public NativeArray<float4> positions;
	[ReadOnly] public NativeArray<int> bodyIDs;
	[ReadOnly] public float3 cameraPos;
	[ReadOnly] public float simulationDistanceSq;

	[WriteOnly] public NativeArray<bool> visibilityResults;

	public void Execute(int i)
	{
		int id = bodyIDs[i];
		float3 pos = positions[id].xyz;
		float distSq = math.distancesq(pos, cameraPos);
		visibilityResults[i] = distSq < simulationDistanceSq;
	}
}
