using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

public class NativeIntArraySystem : SystemBase
{
	[BurstCompile]
	struct ParallelWriteRangeJob : IJobParallelFor
	{
		public Random random;
		// See the previous part on how to add support for [DeallocateOnJobCompletion].
		[DeallocateOnJobCompletion] public NativeIntArray array;

		public void Execute(int index)
		{
			array[index] = random.NextInt();
		}
	}

	protected override void OnUpdate()
	{
		NativeIntArray myArray = new NativeIntArray(1024, Allocator.TempJob);

		// Fill myArray with random values.
		JobHandle jobHandle = new ParallelWriteRangeJob()
		{
			random = new Random((uint)UnityEngine.Random.Range(0, int.MaxValue)),
			array = myArray
		}.Schedule(myArray.Length, 64, Dependency); // Schedule with a batch size of 64.

		Dependency = jobHandle;
	}
}