+++
title= "Custom Native Container [Part 5]: ParallelFor Using ParallelWriter With Thread Index"
author= ["Menno"]
tags=[ "dots","ecs","csharp","intermediate","native container"]
categories=["dots","ecs","unity"]
series = ["Unity's DOTS Custom Native Container"]
date= 2020-03-18T11:03:02+01:00
description="This article will show how to add support for parallel writing to a custom native container using the thread index. This article is a followup of part 4."
+++

{{< figure src="/CustomNativeContainer/NativeSummedFloat3Layout.png#center">}}  
## Introduction
Previously in this series we looked into creating our own custom native container and adding support for features such as deallocation on job completion and multiple ways to add parallel jobs support. In this part we will look into yet another way to add support for parallel writing using `[NativeSetThreadIndex]`.  
This article won't use code from previous articles, but will instead implement a completely new native container. It is therefor assumed that you understand how to do this. If not, you can go back and read the previous articles in this series.

[The final result of this article can be found here.](https://github.com/Eothaun/DOTS-Playground/blob/master/Assets/Articles/CustomNativeContainer/NativeSummedFloat3.cs)
[A generic version of the container made in this article can be found here.](https://github.com/Eothaun/DOTS-Playground/blob/master/Assets/Articles/CustomNativeContainer/NativeValue.cs)

## 1) NativeSummedFloat3 Setup
The container we will implement is called `NativeSummedFloat3`. This container holds a single float3, but allows multiple threads to add to it in parallel. This can be useful when for instance calculating the average position of a large set of entities.  

In the code below we do all the basic setup for our custom container. But interesting to note is the amount of memory we allocate. We will be allocating a single cache line of each worker thread. This allows us to make our container thread safe. By having each thread write to it's own part of memory, the cache line, there will never by multiple threads writing to the same memory. It also allows for better cache access, giving a performance optimization. The downside is that we will be allocating a lot of memory (8Kb in total as of writing). We can't allocate anything less than a single cache line per worker thread, because the CPU will always load a single cache line when accessing data.

```csharp {hl_lines=[43]}
using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

[NativeContainer]
[NativeContainerSupportsDeallocateOnJobCompletion]
[StructLayout(LayoutKind.Sequential)]
public unsafe struct NativeSummedFloat3 : IDisposable
{
	[NativeDisableUnsafePtrRestriction] internal void* m_Buffer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
	internal AtomicSafetyHandle m_Safety;
	[NativeSetClassTypeToNullOnSchedule] internal DisposeSentinel m_DisposeSentinel;
#endif

	internal Allocator m_AllocatorLabel;

	public NativeSummedFloat3(Allocator allocator)
	{
		// Safety checks
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		if (allocator <= Allocator.None)
			throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));

		// There are other checks you might want to perform when working with generic containers and cache lines.
		/*
		if (!UnsafeUtility.IsBlittable<T>())
			throw new ArgumentException(string.Format("{0} used in NativeValue<{0}> must be blittable", typeof(T)));
		if (UnsafeUtility.SizeOf<T>() > JobsUtility.CacheLineSize)
			throw new ArgumentException(string.Format("{0} used in NativeValue<{0}> had a size of {1} which is greater than the maximum size of {2}", typeof(T), UnsafeUtility.SizeOf<T>(), JobsUtility.CacheLineSize));
		*/

		DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0, allocator);
#endif

		// Allocate a cache line for each worker thread.
		m_Buffer = UnsafeUtility.Malloc(JobsUtility.CacheLineSize * JobsUtility.MaxJobThreadCount, JobsUtility.CacheLineSize, allocator);
		m_AllocatorLabel = allocator;
		Value = float3.zero;
	}

	// Allows NativeSummedFloat3 to be cast to float3.
	public static implicit operator float3(NativeSummedFloat3 value) { return value.Value; }

	/*
	 * ... Next Code ...
	 */
```

Lets get all the boring code out of the way by immediately adding the end part of our `NativeSummedFloat3` struct. Again, more about how this code works can be found in previous parts of this series.
```csharp {linenostart=138}
	/*
	 * ... Previous Code ...
	 */

	[WriteAccessRequired]
	public void Dispose()
	{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		if (!UnsafeUtility.IsValidAllocator(m_AllocatorLabel))
			throw new InvalidOperationException("The NativeSummedFloat3 can not be Disposed because it was not allocated with a valid allocator.");

		DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif

		// Free the allocated memory and reset our variables.
		UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
		m_Buffer = null;
	}

	public unsafe JobHandle Dispose(JobHandle inputDeps)
	{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		if (!UnsafeUtility.IsValidAllocator(m_AllocatorLabel))
			throw new InvalidOperationException("The NativeSummedFloat3 can not be Disposed because it was not allocated with a valid allocator.");

		// DisposeSentinel needs to be cleared on the main thread.
		DisposeSentinel.Clear(ref m_DisposeSentinel);
#endif

		NativeSummedFloat3DisposeJob disposeJob = new NativeSummedFloat3DisposeJob()
		{
			Data = new NativeSummedFloat3Dispose() { m_Buffer = m_Buffer, m_AllocatorLabel = m_AllocatorLabel }
		};
		JobHandle result = disposeJob.Schedule(inputDeps);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
		AtomicSafetyHandle.Release(m_Safety);
#endif

		m_Buffer = null;
		return result;
	}
}

[NativeContainer]
internal unsafe struct NativeSummedFloat3Dispose
{
	[NativeDisableUnsafePtrRestriction] internal void* m_Buffer;
	internal Allocator m_AllocatorLabel;
	public void Dispose() { UnsafeUtility.Free(m_Buffer, m_AllocatorLabel); }
}

[BurstCompile]
internal struct NativeSummedFloat3DisposeJob : IJob
{
	internal NativeSummedFloat3Dispose Data;
	public void Execute() { Data.Dispose(); }
}
```

## 2) Single Thread Getter And Setter
Lets implement a getter and setter, that can only be accessed from a single thread. The getter here is interesting. We loop over each cache line and add the values together for the final result. We use `ReadArrayElementWithStride` because our array elements are the size of a cache line, but we're only interested in the float3 stored at the beginning.  
The setter first resets all cache lines to 0 and than add the value. We will have a look at these methods next.
```csharp {linenostart=48}
	/*
	 * ... Other Code ...
	 */

	public float3 Value
	{
		get
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
			// Sum the values stored on each worker threads cache line.
			float3 result = UnsafeUtility.ReadArrayElement<float3>(m_Buffer, 0);
			for (int i = 1; i < JobsUtility.MaxJobThreadCount; i++)
				result += UnsafeUtility.ReadArrayElementWithStride<float3>(m_Buffer, i, JobsUtility.CacheLineSize);

			return result;
		}

		[WriteAccessRequired]
		set
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
			Reset();
			AddValue(value);
		}
	}

	/*
	 * ... Next Code ...
	 */
```

## 3) Single Thread Methods
The `AddValue` and `Reset` methods access the cache lines in a similar manner as our getter. We don't have to worry about multiple writers yet, so we can use `WriteArrayElement` and just write to the first cache line. For `Reset` however we need to use `WriteArrayElementWithStride` again because our elements are the size of a cache line.
```csharp {linenostart=74}
	/*
	 * ... Previous Code ...
	 */

	[WriteAccessRequired]
	public void AddValue(float3 value)
	{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
		// Add a value to the sum. We're writing from a single thread, so we'll write to the first cache line.
		float3 current = UnsafeUtility.ReadArrayElement<float3>(m_Buffer, 0);
		current += value;
		UnsafeUtility.WriteArrayElement(m_Buffer, 0, current);
	}

	[WriteAccessRequired]
	public void Reset()
	{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
		// Reset each worker threads cache line to float3.zero.
		for (int i = 0; i < JobsUtility.MaxJobThreadCount; i++)
			UnsafeUtility.WriteArrayElementWithStride(m_Buffer, i, JobsUtility.CacheLineSize, float3.zero);
	}

	/*
	 * ... Next Code ...
	 */
```

## 4) Parallel Writer With Thread Index
Now for the fun part, parallel writing. We add code within the `NativeSummedFloat3` struct for creating a parallel writer object, as explained in previous articles. But interesting to note is `[NativeSetThreadIndex]` and the `m_ThreadIndex` variable. **Watch out as naming is important here!** This variable will receive the thread index when its scheduled with a job. We than use that variable as index into the cache line to read an write from.
```csharp {linenostart=97, hl_lines=[15,16,"25-27"]}
	/*
	 * ... Previous Code ...
	 */

	[NativeContainerIsAtomicWriteOnly]
	[NativeContainer]
	unsafe public struct ParallelWriter
	{
		[NativeDisableUnsafePtrRestriction] internal void* m_Buffer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
		internal AtomicSafetyHandle m_Safety;
#endif

		[NativeSetThreadIndex]
		internal int m_ThreadIndex;

		[WriteAccessRequired]
		public void AddValue(float3 value)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
			// Add a value to the sum. We're writing in parallel, so we'll write to the cache line assigned to this thread.
			float3 current = UnsafeUtility.ReadArrayElementWithStride<float3>(m_Buffer, m_ThreadIndex, JobsUtility.CacheLineSize);
			current += value;
			UnsafeUtility.WriteArrayElementWithStride(m_Buffer, m_ThreadIndex, JobsUtility.CacheLineSize, current);
		}
	}

	public ParallelWriter AsParallelWriter()
	{
		ParallelWriter writer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
		AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
		writer.m_Safety = m_Safety;
		AtomicSafetyHandle.UseSecondaryVersion(ref writer.m_Safety);
#endif
		writer.m_Buffer = m_Buffer;
		writer.m_ThreadIndex = 0; // Thread index will be set by the job schedular later.

		return writer;
	}

	/*
	 * ... More Code ...
	 */
```

## Usage
That's all we have to do! With this we have a created a custom native container that allows for parallel writing by making use of the thread index. The code below shows how we can use this to calculate the average position of all entities with a `LocalToWorld` component in the scene.
```csharp
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
```

## Conclusion
In this article we wrote a new custom native container that used thread index for parallel writing by assigning each thread it's own memory/cache line. This allowed us to create a float3 that can be written to in parallel. But we can also make this container generic, to allow for any value to be operated on in parallel (as long as they are smaller than a cache line). The generic version of this container can be found [here](https://github.com/Eothaun/DOTS-Playground/blob/master/Assets/Articles/CustomNativeContainer/NativeValue.cs) along with an example om how to use it [here](https://github.com/Eothaun/DOTS-Playground/blob/master/Assets/Articles/CustomNativeContainer/NativeValueSystem.cs)  

[The final result of this article can be found here.](https://github.com/Eothaun/DOTS-Playground/blob/master/Assets/Articles/CustomNativeContainer/NativeSummedFloat3.cs)

[Custom Native Container [Part 1]: The Basics]({{< relref "CustomNativeContainerPt1.md" >}})  
[Custom Native Container [Part 2]: Deallocate On Job Completion]({{< relref "CustomNativeContainerPt2.md" >}})  
[Custom Native Container [Part 3]: Parallel Job Using Min Max]({{< relref "CustomNativeContainerPt3.md" >}})  
[Custom Native Container [Part 4]: Parallel Job Using ParallelWriter]({{< relref "CustomNativeContainerPt4.md" >}})  
Custom Native Container [Part 5]: Parallel Job Using ParallelWriter With Thread Index
