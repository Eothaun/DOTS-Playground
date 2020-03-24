using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

public class NativeSummedFloat3System : SystemBase
{
	private EntityQuery localToWorldQuery;

	protected override void OnUpdate()
	{
		NativeSummedFloat3 avgPosition = new NativeSummedFloat3(Allocator.TempJob);
		NativeSummedFloat3.ParallelWriter avgPositionParallelWriter = avgPosition.AsParallelWriter();

		// Sum together all positions of entities with a LocalToWorld component.
		JobHandle jobHandle = Entities.WithName("AvgPositionJob")
			.WithStoreEntityQueryInField(ref localToWorldQuery)
			.ForEach((in LocalToWorld localToWorld) =>
			{
				avgPositionParallelWriter.AddValue(localToWorld.Position);
			}).ScheduleParallel(default);

		jobHandle.Complete();

		// We store the query so we can calculate how many entities have the LocalToWorld component.
		int entityCount = localToWorldQuery.CalculateEntityCount();
		UnityEngine.Debug.Log(avgPosition.Value / entityCount);

		avgPosition.Dispose();
	}
}