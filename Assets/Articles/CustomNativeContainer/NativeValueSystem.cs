using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

public class NativeValueSystem : SystemBase
{
	private EntityQuery localToWorldQuery;

	struct SummedFloat3Operator : INativeValueOperator<float3>
	{
		public float3 getIdentity() { return float3.zero; }
		public void combine(ref float3 a, ref float3 b) { a += b; }
	}

	protected override void OnUpdate()
	{
		NativeValue<float3, SummedFloat3Operator> avgPosition = new NativeValue<float3, SummedFloat3Operator>(Allocator.TempJob);
		NativeValue<float3, SummedFloat3Operator>.ParallelWriter avgPositionParallelWriter = avgPosition.AsParallelWriter();

		JobHandle jobHandle = Entities.WithName("AvgPositionJob")
			.WithStoreEntityQueryInField(ref localToWorldQuery)
			.ForEach((in LocalToWorld localToWorld) => 
			{
				avgPositionParallelWriter.CombineWith(localToWorld.Position);
			}).ScheduleParallel(default);

		jobHandle.Complete();

		UnityEngine.Debug.Log(avgPosition.Value / localToWorldQuery.CalculateEntityCount());
		avgPosition.Dispose();
	}
}