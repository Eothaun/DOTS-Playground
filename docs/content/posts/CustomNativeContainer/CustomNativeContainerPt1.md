+++
title= "Custom Native Container [Part 1]: The Basics"
author= ["Menno"]
tags=[ "dots","ecs","csharp","intermediate","native container"]
categories=["dots","ecs","unity"]
series = ["Unity's DOTS Custom Native Container"]
date= 2020-03-18T10:59:02+01:00
description="This article will show how to create a basic custom native container in Unity DOTS for use with the job system."
+++

## Introduction
Native containers are used for data communication between jobs. Unity already provides a set of native containers in their [Collections](https://docs.unity3d.com/Packages/com.unity.collections@latest/) package, such as NativeList, NativeQueue, NativeHashMap, etc. But when you need something more custom, you can write your own native container.  

In this article we will write such a custom container that can be used with jobs. In subsequent articles we will look into adding more advanced features to this container such as adding support for parallel jobs. These articles will not be about how to write a *good* container type, but rather will seek to demonstrate all the features that can be implemented when writing a custom native container. This article expects basic knowledge of pointers and memory management.

[The final result of this article can be found here.](https://github.com/Eothaun/DOTS-Playground/commit/5c00dadb86cc68ed76f329f8b8a49a7249cd475d#diff-4107cbc15e6b7565cf1a71565ac1e755)

## NativeIntArray
The container we will be implementing is called NativeIntArray. It is a fixed size array of integers, essentially the same as `NativeArray<int>`. We will purposely use such a simple example so we can focus on the actual native container implementation. The basic structure of this container is shown below. We will override parts of it to turn into a native container.
```csharp
// We will be working with pointers, so our struct needs to be unsafe.
public unsafe struct NativeIntArray
{
    internal void* m_Buffer; // Array to hold our integers.
    internal int m_Length;

    public NativeIntArray(int length) { /* Allocate memory... */ }

    public int this[int index] // Getter and setter using NativeIntArray[index].
    {
        get { return *((int*)m_Buffer + index); }
        set { *((int*)m_Buffer + index) = value; }
    }

    public int Increment(int index) { return ++this[index]; }

    public int Decrement(int index) { return --this[index]; }

    public int Add(int index, int value) { return (this[index] += value); } 

	public int Length => m_Length;
}
```

## 1) Member Variables
Lets first define all the member variables and some of the attributes to turn our struct into a native container. **The naming and order of variables is very important here!** Make sure to copy it exactly so you do not get weird results. The rest of the code is explained through comments. Do not be afraid if you do not understand what each line does exactly, having a rough idea of what each part does is more important.
```csharp
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

// Needed to mark as a native container.
[NativeContainer] 
// Ensure our memory layout is the same as the order of our variables.
[StructLayout(LayoutKind.Sequential)] 
public unsafe struct NativeIntArray : IDisposable
{
    // Relax the pointer safety so jobs can schedule with this container.
    [NativeDisableUnsafePtrRestriction] internal void* m_Buffer;
    internal int m_Length;

    // This macro makes sure safety features can be disabled for better performance.
#if ENABLE_UNITY_COLLECTIONS_CHECKS
    // Handle to tell if operations such as reading and writing can be performed safely.
    internal AtomicSafetyHandle m_Safety;

    // Handle to tell if the container has been disposed.
    // This is a managed object. It can be passed along as the job can't dispose the container, 
    // but needs to be (re)set to null on schedule to prevent job access to a managed object.
    [NativeSetClassTypeToNullOnSchedule] internal DisposeSentinel m_DisposeSentinel;
#endif

    // Keep track of which memory was allocated (Allocator.Temp/TempJob/Persistent).
    internal Allocator m_AllocatorLabel;

	/*
	 * ... Next Code ...
	 */
```

## 2) Allocating Memory
Next we will write out constructor and `Allocate` function. The constructor will use the `Allocate` function to allocate memory for our buffer. The code is again explained through added comments.
```csharp {linenostart=30}
	/*
	 * ... Previous Code ...
	 */

    public NativeIntArray(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
    {
        Allocate(length, allocator, out this);

        // Set the memory block to 0 if requested.
        if ((options & NativeArrayOptions.ClearMemory) == NativeArrayOptions.ClearMemory)
            UnsafeUtility.MemClear(m_Buffer, (long)length * UnsafeUtility.SizeOf<int>());
    }

    static void Allocate(int length, Allocator allocator, out NativeIntArray array)
    {
		// Calculate how many bytes are needed.
        long size = UnsafeUtility.SizeOf<int>() * (long)length;

        // Check if this is a valid allocation.
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if (allocator <= Allocator.None)
            throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));

        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be >= 0");

        if (size > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(length), $"Length * sizeof(int) cannot exceed {(object)int.MaxValue} bytes");

        // There are other checks you might want to perform when working with templated containers.
        /* 
        if (!UnsafeUtility.IsBlittable<T>())
           throw new ArgumentException(string.Format("{0} used in NativeCustomArray<{0}> must be blittable", typeof(T)));

        if (!UnsafeUtility.IsValidNativeContainerElementType<T>())
            throw new InvalidOperationException($"{typeof(T)} used in NativeCustomArray<{typeof(T)}> must be unmanaged (contain no managed types) and cannot itself be a native container type.");
        */
#endif

        array = default(NativeIntArray);
        // Allocate memory for our buffer.
        array.m_Buffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<int>(), allocator);
        array.m_Length = length;
        array.m_AllocatorLabel = allocator;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        // Create a dispose sentinel to track memory leaks. 
        // An atomic safety handle is also created automatically.
        DisposeSentinel.Create(out array.m_Safety, out array.m_DisposeSentinel, 1, allocator);
#endif
    }

	/*
	 * ... Next Code ...
	 */
```

## 3) Read/Write Access 
Now we can finally write the functionality to change values in our array. The code for this is pretty straight forward, if not simpler than the original.
```csharp {linenostart=78}
	/*
	 * ... Previous Code ...
	 */

    public int this[int index]
    {
        get
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        	AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return UnsafeUtility.ReadArrayElement<int>(m_Buffer, index);
        }

        [WriteAccessRequired]
        set
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        	AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            UnsafeUtility.WriteArrayElement(m_Buffer, index, value);
        }
    }

    public int Increment(int index) { return ++this[index]; }

    public int Decrement(int index) { return --this[index]; }

    public int Add(int index, int value) { return (this[index] += value); }

	public int Length => m_Length;

	/*
	 * ... Next Code ...
	 */
```

## 4) Disposing
There is one last thing we must not forget before we can use our container, and that is `Dispose`. Dispose can be called to cleanup an free our memory after we are done with our container. 
```csharp {linenostart=106}
	/*
	 * ... Previous Code ...
	 */

	public void Dispose()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if (!UnsafeUtility.IsValidAllocator(m_AllocatorLabel))
            throw new InvalidOperationException("The NativeArray can not be Disposed because it was not allocated with a valid allocator.");

        DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif

		// Free the allocated memory and reset our variables.
        UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
        m_Buffer = null;
        m_Length = 0;
    }
}
```

## Usage
That is it! We now have created a `NativeIntArray` that can be used with jobs like below.
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
		Job.WithName("NativeIntArrayJob").WithCode(() =>
        {
            for (int i = 0; i < myArray.Length; i++)
				myArray.Increment(i);
        }).Run();

        myArray.Dispose();
	}
}
```

## Conclusion
This article showed all the steps involved to create a bare basic native container. But you might have already noticed that a few very useful features are missing. For instance `.WithDeallocateOnJobCompletion` and `[DeallocateOnJobCompletion]` will throw an error that our container does not support this incredibly useful feature. We will implement missing features in the next parts of this series:  

Custom Native Container [Part 1]: The Basics  
[Custom Native Container [Part 2]: Deallocate On Job Completion]({{< relref "CustomNativeContainerPt2.md" >}})  
[Custom Native Container [Part 3]: Parallel Job Using Min Max]({{< relref "CustomNativeContainerPt3.md" >}})  
[Custom Native Container [Part 4]: Parallel Job Using ParallelWriter]({{< relref "CustomNativeContainerPt4.md" >}})  
