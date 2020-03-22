+++
title= "Custom Native Container [Part 2]: Deallocate On Job Completion"
author= ["Menno"]
tags=[ "dots","ecs","csharp","intermediate","native container"]
categories=["dots","ecs","unity"]
series = ["Unity's DOTS Custom Native Container"]
date= 2020-03-18T11:00:02+01:00
description="This article will show how to add support to a custom native container for deallocation on job completion. This article is a followup of part 1."
+++

## Introduction
In the previous article in this series, [Custom Native Container [Part 1]: The Basics]({{< relref "CustomNativeContainerPt1.md" >}}), we looked into how we can create a bare basic custom native container for usage with the job system. In this article we will extend our `NativeIntArray` container to add support for usage with `.WithDeallocateOnJobCompletion` and `[DeallocateOnJobCompletion]`.  

[The result of the previous article can be found here.](https://github.com/Eothaun/DOTS-Playground/commit/5c00dadb86cc68ed76f329f8b8a49a7249cd475d#diff-4107cbc15e6b7565cf1a71565ac1e755)  
[The final result of this article can be found here.](https://github.com/Eothaun/DOTS-Playground/commit/0afe23b3c72c4286029b94ea0dac78b29dd1b8f0#diff-4107cbc15e6b7565cf1a71565ac1e755)

## 1) Enable Support
To enable support for deallocation on job completion we must add the `[NativeContainerSupportsDeallocateOnJobCompletion]` attribute to our container struct. We will also use this opportunity to have a quick reminder of what our container roughly looked like: a simple array of integers.
```csharp {hl_lines=[10,11]}
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

// Enable support for ".WithDeallocateOnJobCompletion" and "[DeallocateOnJobCompletion]".
[NativeContainerSupportsDeallocateOnJobCompletion]
[NativeContainer] 
[StructLayout(LayoutKind.Sequential)] 
public unsafe struct NativeIntArray : IDisposable
{
    [NativeDisableUnsafePtrRestriction] internal void* m_Buffer;
    internal int m_Length;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    internal AtomicSafetyHandle m_Safety;
    [NativeSetClassTypeToNullOnSchedule] internal DisposeSentinel m_DisposeSentinel;
#endif

    internal Allocator m_AllocatorLabel;

    public NativeIntArray(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory) { /* More Code */ }

    static void Allocate(int length, Allocator allocator, out NativeIntArray array)
    {
        long size = UnsafeUtility.SizeOf<int>() * (long)length;

		/* More Code */

        array = default(NativeIntArray);
        // Allocate memory for our buffer.
        array.m_Buffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<int>(), allocator);
        array.m_Length = length;
        array.m_AllocatorLabel = allocator;

		/* More Code */
    }

	/* More Code */
```

## 2) JobHandle Dispose
Next we will add a new dispose function to our container struct. This dispose function will not deallocate our container immediately, but will instead return a job handle that can be scheduled later. This is how deallocate on job completion works, by scheduling another job to do the cleanup once our job is completed.
```csharp {linenostart=121}
    /*
	 * ... More Code ...
	 */

	public unsafe JobHandle Dispose(JobHandle inputDeps)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        // DisposeSentinel needs to be cleared on the main thread.
        DisposeSentinel.Clear(ref m_DisposeSentinel);
#endif

        // Create a job to dispose of our container and pass a copy of our pointer to it.
        NativeCustomArrayDisposeJob disposeJob = new NativeCustomArrayDisposeJob()
        {
            Data = new NativeCustomArrayDispose() 
            { 
                m_Buffer = m_Buffer,
                m_AllocatorLabel = m_AllocatorLabel
            }
        };
        JobHandle result = disposeJob.Schedule(inputDeps);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.Release(m_Safety);
#endif
        
        m_Buffer = null;
        m_Length = 0;

        return result;
    }

    /*
	 * ... More Code ...
	 */
```

## 3) NativeCustomArrayDisposeJob And NativeCustomArrayDispose
As you may have noticed, inside our `Dispose` function we make use of two new structs. These need to be defined outside out container struct. `NativeCustomArrayDispose` is used to hold a copy of our container pointer and `NativeCustomArrayDisposeJob` will call `Dispose`. Whenever the job gets scheduled, it will internally make sure that no other job is reading or writing to our container.
```csharp {linenostart=150}
/*
* ... More Code ...
*/

[NativeContainer]
internal unsafe struct NativeCustomArrayDispose
{
    // Relax the pointer safety so jobs can schedule with this struct.
    [NativeDisableUnsafePtrRestriction] internal void* m_Buffer;
    internal Allocator m_AllocatorLabel;

    public void Dispose()
    {
        // Free the allocated memory
        UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
    }
}

[BurstCompile]
internal struct NativeCustomArrayDisposeJob : IJob
{
    internal NativeCustomArrayDispose Data;

    public void Execute()
    {
        Data.Dispose();
    }
}
```

## Usage
And that is it! We now have added support for parallel jobs to our `NativeIntArray`. An example of this is shown below.
```csharp
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
```

## Conclusion
This article showed how to add support for `.WithDeallocateOnJobCompletion` and `[DeallocateOnJobCompletion]`. In the next part(s) we will continue to add features to our `NativeIntArray` by supporting parallel jobs.    

[Custom Native Container [Part 1]: The Basics]({{< relref "CustomNativeContainerPt1.md" >}})  
Custom Native Container [Part 2]: Deallocate On Job Completion  
[Custom Native Container [Part 3]: Parallel Job Using Min Max]({{< relref "CustomNativeContainerPt3.md" >}})  
[Custom Native Container [Part 4]: Parallel Job Using ParallelWriter]({{< relref "CustomNativeContainerPt4.md" >}})  
