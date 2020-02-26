using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

// NativeContainerSupportsMinMaxWriteRestriction enables IJobParallelFor usage
[NativeContainerSupportsMinMaxWriteRestriction]
[NativeContainerSupportsDeallocateOnJobCompletion]
[StructLayout(LayoutKind.Sequential)]
[NativeContainer]
public unsafe struct NativeMinHeap<TValue, TPriority> : IDisposable
    where TValue : unmanaged
    where TPriority : IComparable
{
    // Container variables must be named and ordered like this
    [NativeDisableUnsafePtrRestriction]
    internal void* m_Buffer;
    internal int m_Length; // Not the actual length but the buffer capacity

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    // NativeContainerSupportsMinMaxWriteRestriction container is expected to safty check ranges
    internal int m_MinIndex;
    internal int m_MaxIndex;

    // Handle to tell what operations can be performed safely
    internal AtomicSafetyHandle m_Safety;

    // Handle to tell if the list has been disposed
    [NativeSetClassTypeToNullOnSchedule]
    internal DisposeSentinel m_DisposeSentinel;
#endif

    internal Allocator m_AllocatorLabel;
    //

    // Heap variables
    private int size;

    public NativeMinHeap(int capacity, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
    {
        // Allocate the buffer
        Allocate(capacity, allocator, out this);

        // Clear the buffer if passed as argument
        if ((options & NativeArrayOptions.ClearMemory) == NativeArrayOptions.ClearMemory)
            UnsafeUtility.MemClear(m_Buffer, (long)capacity * UnsafeUtility.SizeOf<NativeMinHeapNode<TValue, TPriority>>());
    }

    private static void Allocate(int capacity, Allocator allocator, out NativeMinHeap<TValue, TPriority> nativeMinHeap)
    {
        var size = (long)UnsafeUtility.SizeOf<NativeMinHeapNode<TValue, TPriority>>() * capacity;

        // Check for valid allocation
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if (allocator <= Allocator.None)
            throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));

        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Length must be >= 0");

        if (size > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(capacity), $"Length * sizeof(T) cannot exceed {(object)int.MaxValue} bytes");

        if (!UnsafeUtility.IsBlittable<TValue>())
            throw new ArgumentException(string.Format("{0} used in NativeMinHeap<{0}> must be blittable", typeof(TValue)));
#endif

        // Allocate buffer
        nativeMinHeap.m_Buffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<NativeMinHeapNode<TValue, TPriority>>(), allocator);
        nativeMinHeap.m_Length = capacity;
        nativeMinHeap.m_AllocatorLabel = allocator;
        nativeMinHeap.size = 0;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        nativeMinHeap.m_MinIndex = 0;
        nativeMinHeap.m_MaxIndex = capacity - 1;
        DisposeSentinel.Create(out nativeMinHeap.m_Safety, out nativeMinHeap.m_DisposeSentinel, 1, allocator);
#endif
    }

    public NativeMinHeapNode<TValue, TPriority> this[int index]
    {
        get
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < m_MinIndex || index > m_MaxIndex)
                throw new IndexOutOfRangeException($"Index {index} is out of range of '{this.m_Length}' length.");
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif

            return UnsafeUtility.ReadArrayElement<NativeMinHeapNode<TValue, TPriority>>(m_Buffer, index);
        }
        set
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < m_MinIndex || index > m_MaxIndex)
                throw new IndexOutOfRangeException($"Index {index} is out of range of '{this.m_Length}' length.");
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif

            UnsafeUtility.WriteArrayElement(m_Buffer, index, value);
        }
    }

    public bool IsCreated
    {
        get { return m_Buffer != null; }
    }

    public bool Empty()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        return size == 0;
    }

    public void Push(TValue value, TPriority priority)
    {
        Push(new NativeMinHeapNode<TValue, TPriority>(value, priority));
    }

    public void Push(NativeMinHeapNode<TValue, TPriority> node)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if (size == m_Length)
            throw new IndexOutOfRangeException($"Capacity Reached");
        AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif

        UnsafeUtility.WriteArrayElement(m_Buffer, size, node);
        size++;
        BubbleUp();
    }

    public NativeMinHeapNode<TValue, TPriority> Pop()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if (size == 0)
            throw new IndexOutOfRangeException($"Size is zero");
        AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        var result = this[0];
        this[0] = this[size - 1];
        size--;
        BubbleDown();

        return result;
    }

    public void Clear()
    {
        size = 0;
    }

    private void Swap(int firstIndex, int secondIndex)
    {
        var temp = this[firstIndex];
        this[firstIndex] = this[secondIndex];
        this[secondIndex] = temp;
    }

    private void BubbleUp()
    {
        int index = size - 1;
        int parentIndex = (index - 1) / 2;
        while (index != 0 && this[index].Priority.CompareTo(this[parentIndex].Priority) < 0)
        {
            Swap(parentIndex, index);
            index = parentIndex;
            parentIndex = (index - 1) / 2;
        }
    }

    private void BubbleDown()
    {
        int index = 0;
        int leftIndex = 2 * index + 1;
        while (leftIndex < size)
        {
            int rightIndex = 2 * index + 2;
            if (rightIndex < size && this[rightIndex].Priority.CompareTo(this[leftIndex].Priority) < 0)
                leftIndex = rightIndex;

            if (this[leftIndex].Priority.CompareTo(this[index].Priority) >= 0)
                break;

            Swap(leftIndex, index);
            index = leftIndex;
            leftIndex = 2 * index + 1;
        }
    }

    public void Dispose()
    {
        if (!UnsafeUtility.IsValidAllocator(m_AllocatorLabel))
            throw new InvalidOperationException("The NativeArray can not be Disposed because it was not allocated with a valid allocator.");

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif

        UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
        m_Buffer = null;
        m_Length = 0;
    }

    public unsafe JobHandle Dispose(JobHandle inputDeps)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        DisposeSentinel.Clear(ref m_DisposeSentinel);
#endif

        NativeMinHeapDisposeJob disposeJob = new NativeMinHeapDisposeJob()
        {
            Data = new NativeMinHeapDispose()
            {
                m_Buffer = m_Buffer,
                m_AllocatorLabel = m_AllocatorLabel,
                m_Safety = m_Safety
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
}

public struct NativeMinHeapNode<TValue, TPriority> 
    where TValue : unmanaged 
    where TPriority : IComparable
{
    public NativeMinHeapNode(TValue value, TPriority priority)
    {
        Value = value;
        Priority = priority;
        Next = -1;
    }

    public TValue Value { get; set; }
    public TPriority Priority { get; set; }
    public int Next { get; set; }
}

[NativeContainer]
internal unsafe struct NativeMinHeapDispose
{
    [NativeDisableUnsafePtrRestriction] internal void* m_Buffer;
    internal Allocator m_AllocatorLabel;

    internal AtomicSafetyHandle m_Safety;

    public void Dispose()
    {
        UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
    }
}

[BurstCompile]
struct NativeMinHeapDisposeJob : IJob
{
    internal NativeMinHeapDispose Data;

    public void Execute()
    {
        Data.Dispose();
    }
}