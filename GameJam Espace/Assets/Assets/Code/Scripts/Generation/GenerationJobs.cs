using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct LODVisibilityJob : IJobParallelFor
{
	[ReadOnly] public NativeArray<double4> positions;
	[ReadOnly] public NativeArray<int> bodyIDs;
	[ReadOnly] public double3 cameraPos;
	[ReadOnly] public double simulationDistanceSq;

	[WriteOnly] public NativeArray<bool> visibilityResults;

	public void Execute(int i)
	{
		int id = bodyIDs[i];
		double3 pos = positions[id].xyz;
		double distSq = math.distancesq(pos, cameraPos);
		visibilityResults[i] = distSq < simulationDistanceSq;
	}
}
