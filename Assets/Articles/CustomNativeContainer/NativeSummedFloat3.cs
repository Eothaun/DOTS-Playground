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

		// There are other checks you might want to perform when working with generic containers.
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
		writer.m_ThreadIndex = 0;

		return writer;
	}

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

		// Create a job to dispose of our container and pass a copy of our pointer to it.
		NativeSummedFloat3DisposeJob disposeJob = new NativeSummedFloat3DisposeJob()
		{
			Data = new NativeSummedFloat3Dispose()
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

		return result;
	}
}

[NativeContainer]
internal unsafe struct NativeSummedFloat3Dispose
{
	[NativeDisableUnsafePtrRestriction] internal void* m_Buffer;
	internal Allocator m_AllocatorLabel;

	public void Dispose()
	{
		// Free the allocated memory
		UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
	}
}

[BurstCompile]
internal struct NativeSummedFloat3DisposeJob : IJob
{
	internal NativeSummedFloat3Dispose Data;

	public void Execute()
	{
		Data.Dispose();
	}
}