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