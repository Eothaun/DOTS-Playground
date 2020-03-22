using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

public class NativeIntArraySystem : SystemBase
{
	protected override void OnUpdate()
	{
		NativeIntArray myArray = new NativeIntArray(100, Allocator.TempJob);
		Job.WithName("NativeIntArrayJob")
			.WithDeallocateOnJobCompletion(myArray)
			.WithCode(() =>
			{
				for (int i = 0; i < myArray.Length; i++)
					myArray.Increment(i);
			}).Run();
	}
}