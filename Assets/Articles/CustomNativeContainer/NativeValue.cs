using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

public interface INativeValueOperator<T>
	where T : unmanaged
{
	T getIdentity();
	void combine(ref T a, ref T b);
}

[NativeContainer]
[NativeContainerSupportsDeallocateOnJobCompletion]
[StructLayout(LayoutKind.Sequential)]
public unsafe struct NativeValue<T, Op> : IDisposable 
	where T : unmanaged
	where Op : unmanaged, INativeValueOperator<T>
{
	[NativeDisableUnsafePtrRestriction] internal void* m_Buffer;
	internal Op m_Operator;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
	internal AtomicSafetyHandle m_Safety;
	[NativeSetClassTypeToNullOnSchedule] internal DisposeSentinel m_DisposeSentinel;
#endif

	internal Allocator m_AllocatorLabel;

	public NativeValue(Allocator allocator, Op valueOperator = default(Op))
	{
		// Safety checks
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		if (allocator <= Allocator.None)
			throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));

		if (!UnsafeUtility.IsBlittable<T>())
			throw new ArgumentException(string.Format("{0} used in NativeValue<{0}, {1}> must be blittable", typeof(T), typeof(Op)));
		
		if (UnsafeUtility.SizeOf<T>() > JobsUtility.CacheLineSize)
			throw new ArgumentException(string.Format("{0} used in NativeValue<{0}, {1}> had a size of {2} which is greater than the maximum size of {3}", typeof(T), typeof(Op), UnsafeUtility.SizeOf<T>(), JobsUtility.CacheLineSize));

		if (UnsafeUtility.SizeOf<Op>() > 1)
			throw new ArgumentException(string.Format("{0} used in NativeValue<{1}, {0}> had a size of {2} which is greater than 1. Access to {0} is not thread safe and can therefor not contain any member variables", typeof(Op), typeof(T), UnsafeUtility.SizeOf<Op>(), JobsUtility.CacheLineSize));

		DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0, allocator);
#endif

		// Allocate a cache line for each worker thread.
		m_Buffer = UnsafeUtility.Malloc(JobsUtility.CacheLineSize * JobsUtility.MaxJobThreadCount, JobsUtility.CacheLineSize, allocator);
		m_AllocatorLabel = allocator;
		m_Operator = valueOperator;

		Value = m_Operator.getIdentity();
	}

	public static implicit operator T(NativeValue<T, Op> value)
	{
		return value.Value;
	}

	public T Value
	{
		get
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
			// Combine the values stored on each worker threads cache line.
			T result = UnsafeUtility.ReadArrayElement<T>(m_Buffer, 0);
			for (int i = 1; i < JobsUtility.MaxJobThreadCount; i++)
			{
				T element = UnsafeUtility.ReadArrayElementWithStride<T>(m_Buffer, i, JobsUtility.CacheLineSize);
				m_Operator.combine(ref result, ref element);
			}

			return result;
		}

		[WriteAccessRequired]
		set
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
			Reset();
			CombineWith(value);
		}
	}

	[WriteAccessRequired]
	public void CombineWith(T value)
	{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
		// Write a value to the container and combine it with the value already present.
		T current = UnsafeUtility.ReadArrayElement<T>(m_Buffer, 0);
		m_Operator.combine(ref current, ref value);
		UnsafeUtility.WriteArrayElement(m_Buffer, 0, current);
	}

	[WriteAccessRequired]
	public void Reset()
	{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
		// Reset each worker threads cache line.
		for (int i = 0; i < JobsUtility.MaxJobThreadCount; i++)
		{
			UnsafeUtility.WriteArrayElementWithStride(m_Buffer, i, JobsUtility.CacheLineSize, m_Operator.getIdentity());
		}
	}

	[NativeContainerIsAtomicWriteOnly]
	[NativeContainer]
	unsafe public struct ParallelWriter
	{
		[NativeDisableUnsafePtrRestriction] internal void* m_Buffer;
		internal Op m_Operator;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
		internal AtomicSafetyHandle m_Safety;
#endif

		[NativeSetThreadIndex]
		internal int m_ThreadIndex;

		[WriteAccessRequired]
		public void CombineWith(T value)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
			T current = UnsafeUtility.ReadArrayElementWithStride<T>(m_Buffer, m_ThreadIndex, JobsUtility.CacheLineSize);
			m_Operator.combine(ref current, ref value);
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
		writer.m_Operator = m_Operator;
		writer.m_ThreadIndex = 0;

		return writer;
	}

	[WriteAccessRequired]
	public void Dispose()
	{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		if (!UnsafeUtility.IsValidAllocator(m_AllocatorLabel))
			throw new InvalidOperationException(string.Format("The NativeValue<{0}, {1}> can not be Disposed because it was not allocated with a valid allocator.", typeof(T), typeof(Op)));

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
			throw new InvalidOperationException(string.Format("The NativeValue<{0}, {1}> can not be Disposed because it was not allocated with a valid allocator.", typeof(T), typeof(Op)));

		// DisposeSentinel needs to be cleared on the main thread.
		DisposeSentinel.Clear(ref m_DisposeSentinel);
#endif

		// Create a job to dispose of our container and pass a copy of our pointer to it.
		NativeValueDisposeJob disposeJob = new NativeValueDisposeJob()
		{
			Data = new NativeValueDispose()
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
internal unsafe struct NativeValueDispose
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
internal struct NativeValueDisposeJob : IJob
{
	internal NativeValueDispose Data;

	public void Execute()
	{
		Data.Dispose();
	}
}