using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

public class NativeIntArraySystem : SystemBase
{
	[BurstCompile]
	struct ParallelWriteNormalDistributionJob : IJobParallelFor
	{
		public Random random;
		public NativeIntArray.ParallelWriter array;

		public void Execute(int index)
		{
			// Calculate normal distribution.
			double u1 = 1.0 - random.NextDouble();
			double u2 = 1.0 - random.NextDouble();
			double randomStdNormal = math.sqrt(-2.0 * math.log(u1)) * math.sin(2.0 * math.PI * u2);
			double randomNormal = (array.Length / 2) + randomStdNormal * (array.Length / 8);

			// Use the normal distribution to pick an element to increment.
			int arrayIndex = math.clamp((int)randomNormal, 0, array.Length - 1);

			// Use our atomic operation.
			array.Increment(arrayIndex);
		}
	}

	protected override void OnUpdate()
	{
		NativeIntArray myArray = new NativeIntArray(100, Allocator.TempJob);

		// Fill myArray with normal distribution values.
		JobHandle jobHandle = new ParallelWriteNormalDistributionJob()
		{
			random = new Random((uint)UnityEngine.Random.Range(0, int.MaxValue)),
			array = myArray.AsParallelWriter()
		}.Schedule(10000, 64); // Run our job a 10000 times in batches of 64 (values chosen randomly).

		jobHandle.Complete();

		// Draw each element in myArray as a bar graph where it's value is the height of the bar.
		Job.WithName("DrawBarGraph")
			.WithReadOnly(myArray)
			.WithoutBurst()
			.WithCode(() =>
			{
				for (int i = 0; i < myArray.Length; i++)
				{
					float barWidth = 1.0f;
					float barHeight = (myArray[i] / 40.0f) * 10.0f;
					DrawBar(new float2(i * barWidth, 0), new float2(barWidth, barHeight));
				}
			}).Run();


		myArray.Dispose();
	}

	private void DrawBar(float2 position, float2 size)
	{
		UnityEngine.Color color = UnityEngine.Color.red;
		float3 lowerBound = new float3(position.xy, 0);

		UnityEngine.Debug.DrawLine(lowerBound, lowerBound + new float3(size.x, 0, 0), color);
		UnityEngine.Debug.DrawLine(lowerBound, lowerBound + new float3(0, size.y, 0), color);
		UnityEngine.Debug.DrawLine(lowerBound, lowerBound + new float3(size.xy, 0), color);

		lowerBound += new float3(size.xy, 0);
		UnityEngine.Debug.DrawLine(lowerBound, lowerBound + new float3(-size.x, 0, 0), color);
		UnityEngine.Debug.DrawLine(lowerBound, lowerBound + new float3(0, -size.y, 0), color);
	}
}