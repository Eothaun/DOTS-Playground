+++
title= "Custom Native Container [Part 3]: Parallel Job Using Min Max"
author= ["Menno"]
tags=[ "dots","ecs","csharp","intermediate","native container"]
categories=["dots","ecs","unity"]
series = ["Unity's DOTS Custom Native Container"]
date= 2020-03-18T11:01:02+01:00
description="This article will show how to add support for parallel jobs by constricting them to operate over a subset of indices, preventing simultaneous reading and writing. This article is a followup of part 2."
+++

{{< figure src="/CustomNativeContainer/IJobParallelForDiagram.png#center">}}  
## Introduction
In previous parts of this series we looked into how to create a basic custom native container that can be used with jobs. This article will improve our `NativeIntArray` container to add support for parallel jobs. This is done by using a pattern where the job is split into ranges and each job is only allowed to operate on this range. This limits the array access to the index passed through `Execute(int index)`. More information about how these jobs are schedualed behind the scenes can be found in the Unity documentation [here](https://docs.unity3d.com/Manual/JobSystemParallelForJobs.html).  

[The result of the previous article can be found here.](https://github.com/Eothaun/DOTS-Playground/commit/0afe23b3c72c4286029b94ea0dac78b29dd1b8f0#diff-4107cbc15e6b7565cf1a71565ac1e755)  
[The final result of this article can be found here.](https://github.com/Eothaun/DOTS-Playground/commit/62bf8506ea598ab6ac32eb158efce4b3f90d929b#diff-4107cbc15e6b7565cf1a71565ac1e755)

## 1) Enable Support
We add the `[NativeContainerSupportsMinMaxWriteRestriction]` tag to enable support for this kind of parallel job. We also have to create `m_MinIndex` and `m_MaxIndex` variables and initialize them with the entire range of our array. These variables are required for safety checking. **Watch out, the naming and order of variables is very important here!**  
We will also use this opportunity to have a quick reminder of what our container roughly looked like: a simple array of integers.
```csharp {hl_lines=["9-11", "21-24", "46-48"]}
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

// This enables support for parallel job exection where each worker thread 
// is only allowed to operation on a range of indices between min and max.
[NativeContainerSupportsMinMaxWriteRestriction]
[NativeContainerSupportsDeallocateOnJobCompletion]
[NativeContainer]
[StructLayout(LayoutKind.Sequential)] 
public unsafe struct NativeIntArray : IDisposable
{
    [NativeDisableUnsafePtrRestriction] internal void* m_Buffer;
    internal int m_Length;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
	// NativeContainerSupportsMinMaxWriteRestriction expects the passed ranges it can operate on to be checked for safety.
	// The range is passed to the container when an parallel job schedules it's batch jobs.
	internal int m_MinIndex;
    internal int m_MaxIndex;

    internal AtomicSafetyHandle m_Safety;
    [NativeSetClassTypeToNullOnSchedule] internal DisposeSentinel m_DisposeSentinel;
#endif

    internal Allocator m_AllocatorLabel;

    public NativeIntArray(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory){ /* More Code */ }

    static void Allocate(int length, Allocator allocator, out NativeIntArray array)
    {
        long size = UnsafeUtility.SizeOf<int>() * (long)length;

		/* More Code */

        array = default(NativeIntArray);
        array.m_Buffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<int>(), allocator);
        array.m_Length = length;
        array.m_AllocatorLabel = allocator;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        // By default the job can operate over the entire range.
        array.m_MinIndex = 0;
        array.m_MaxIndex = length - 1;

        DisposeSentinel.Create(out array.m_Safety, out array.m_DisposeSentinel, 1, allocator);
#endif
    }

	/*
	 * ... Next Code ...
	 */
```

## 2) Range checking
The only other change we need to make to the code is to check if we are within range when accessing an element in the array. All other functions that access the array in this container use the `[]` operator to do so, therefor it is enough to add our range checks to this operator only.
```csharp {hl_lines=["5-23", 32, 42],linenostart=92}
	/*
	 * ... Previous Code ...
	 */

	// Remove calls to this function if safety is disabled.
	[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
	private void CheckRangeAccess(int index)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		// Check if we're within the range of indices that this parallel batch job operates on.
		if (index < m_MinIndex || index > m_MaxIndex)
        {
            if (index < Length && (m_MinIndex != 0 || m_MaxIndex != Length - 1))
                throw new IndexOutOfRangeException(string.Format(
                    "Index {0} is out of restricted IJobParallelFor range [{1}...{2}] in ReadWriteBuffer.\n" +
                    "ReadWriteBuffers are restricted to only read & write the element at the job index. " +
                    "You can use double buffering strategies to avoid race conditions due to " +
                    "reading & writing in parallel to the same elements from a job.",
                    index, m_MinIndex, m_MaxIndex));

			// This is not a parallel job but the index is still out of range.
			throw new IndexOutOfRangeException(string.Format("Index {0} is out of range of '{1}' Length.", index, Length));
        }
#endif
    }

    public int this[int index]
    {
        get
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            CheckRangeAccess(index);
            return UnsafeUtility.ReadArrayElement<int>(m_Buffer, index);
        }

        [WriteAccessRequired]
        set
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            CheckRangeAccess(index);
            UnsafeUtility.WriteArrayElement(m_Buffer, index, value);
        }
    }

	/*
	* ... More Code ...
	*/
```

## Usage
And that all! We now have added support for deallocation on job completion to our `NativeIntArray`. An example of this is shown below.
```csharp
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
```

## Conclusion
This article showed how to add support for parallel jobs using a pattern where the job is split into ranges. But a limitation of this pattern is that it does not allow for multiple jobs to write to the same index. In the next part we will look how we can make this possible by adding support for `ParallelWriter`.

[Custom Native Container [Part 1]: The Basics]({{< relref "CustomNativeContainerPt1.md" >}})  
[Custom Native Container [Part 2]: Deallocate On Job Completion]({{< relref "CustomNativeContainerPt2.md" >}})  
Custom Native Container [Part 3]: Parallel Job Using Min Max  
[Custom Native Container [Part 4]: Parallel Job Using ParallelWriter]({{< relref "CustomNativeContainerPt4.md" >}})  
