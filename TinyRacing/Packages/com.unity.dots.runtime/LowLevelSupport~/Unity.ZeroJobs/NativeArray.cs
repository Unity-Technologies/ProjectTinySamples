using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Internal;

namespace Unity.Collections
{
    public enum NativeArrayOptions
    {
        UninitializedMemory            = 0,
        ClearMemory                    = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    [NativeContainerSupportsDeallocateOnJobCompletion]
    [NativeContainerSupportsDeferredConvertListToArray]
    [DebuggerDisplay("Length = {Length}")]
    [DebuggerTypeProxy(typeof(NativeArrayDebugView<>))]
    public unsafe struct NativeArray<T> : IDisposable, IEnumerable<T>, IEquatable<NativeArray<T>> where T : struct
    {
        [NativeDisableUnsafePtrRestriction]
        internal void*                    m_Buffer;
        internal int                      m_Length;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal int                      m_MinIndex;
        internal int                      m_MaxIndex;
        internal AtomicSafetyHandle       m_Safety;
        [NativeSetClassTypeToNullOnSchedule]
        internal DisposeSentinel          m_DisposeSentinel;
#endif

        internal Allocator                m_AllocatorLabel;

        public NativeArray(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            Allocate(length, allocator, out this);
            if ((options & NativeArrayOptions.ClearMemory) == NativeArrayOptions.ClearMemory)
                UnsafeUtility.MemClear(m_Buffer, (long)Length * UnsafeUtility.SizeOf<T>());
        }

        public NativeArray(T[] array, Allocator allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (array == null)
                throw new ArgumentNullException(nameof(array));
#endif

            Allocate(array.Length, allocator, out this);
            Copy(array, this);
        }

        public NativeArray(NativeArray<T> array, Allocator allocator)
        {
            Allocate(array.Length, allocator, out this);
            Copy(array, this);
        }

        static void Allocate(int length, Allocator allocator, out NativeArray<T> array)
        {
            var totalSize = UnsafeUtility.SizeOf<T>() * (long)length;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Native allocation is only valid for Temp, Job and Persistent.
            if (allocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be >= 0");

            IsUnmanagedAndThrow();

            // Make sure we cannot allocate more than int.MaxValue (2,147,483,647 bytes)
            // because the underlying UnsafeUtility.Malloc is expecting a int.
            // TODO: change UnsafeUtility.Malloc to accept a UIntPtr length instead to match C++ API
            if (totalSize > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(length), $"Length * sizeof(T) cannot exceed {int.MaxValue} bytes");
#endif

            array = default(NativeArray<T>);
            array.m_Buffer = UnsafeUtility.Malloc(totalSize, UnsafeUtility.AlignOf<T>(), allocator);
            array.m_Length = length;
            array.m_AllocatorLabel = allocator;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            array.m_MinIndex = 0;
            array.m_MaxIndex = length - 1;
            DisposeSentinel.Create(out array.m_Safety, out array.m_DisposeSentinel, 1, allocator);
#endif
        }

        public int Length => m_Length;

        [BurstDiscard]
        internal static void IsUnmanagedAndThrow()
        {
            if (!UnsafeUtility.IsValidNativeContainerElementType<T>())
            {
                throw new InvalidOperationException(
                    $"{typeof(T)} used in NativeArray<{typeof(T)}> must be unmanaged (contain no managed types) and cannot itself be a native container type.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckElementReadAccess(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < m_MinIndex || index > m_MaxIndex)
                FailOutOfRangeError(index);

            // Check versions match and read protection is not flagged
            int version = m_Safety.UncheckedGetNodeVersion();
            if (m_Safety.version != (version & AtomicSafetyNodeVersionMask.VersionAndReadProtect))
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckElementWriteAccess(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < m_MinIndex || index > m_MaxIndex)
                FailOutOfRangeError(index);

            // Check versions match and write protection is not flagged
            int version = m_Safety.UncheckedGetNodeVersion();
            if (m_Safety.version != (version & AtomicSafetyNodeVersionMask.VersionAndWriteProtect))
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckElementReadAccess(index);
                return UnsafeUtility.ReadArrayElement<T>(m_Buffer, index);
            }

            [WriteAccessRequired]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                CheckElementWriteAccess(index);
                UnsafeUtility.WriteArrayElement(m_Buffer, index, value);
            }
        }

        public bool IsCreated => m_Buffer != null;

        [WriteAccessRequired]
        public void Dispose()
        {
            if (m_AllocatorLabel == Allocator.Invalid)
            {
                throw new InvalidOperationException("The NativeArray can not be Disposed because it was not allocated with a valid allocator.");
            }

            if (m_Buffer == null)
            {
                throw new InvalidOperationException("The NativeArray is already disposed.");
            }

            if (m_AllocatorLabel > Allocator.None)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
                UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
                m_AllocatorLabel = Allocator.Invalid;
            }

            m_Buffer = null;
            m_Length = 0;
        }

        /// <summary>
        /// Safely disposes of this container and deallocates its memory when the jobs that use it have completed.
        /// </summary>
        /// <remarks>You can call this function dispose of the container immediately after scheduling the job. Pass
        /// the [JobHandle](https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.html) returned by
        /// the [Job.Schedule](https://docs.unity3d.com/ScriptReference/Unity.Jobs.IJobExtensions.Schedule.html)
        /// method using the `jobHandle` parameter so the job scheduler can dispose the container after all jobs
        /// using it have run.</remarks>
        /// <param name="jobHandle">The job handle or handles for any scheduled jobs that use this container.</param>
        /// <returns>A new job handle containing the prior handles as well as the handle for the job that deletes
        /// the container.</returns>
        public JobHandle Dispose(JobHandle inputDeps)
        {
            if (m_AllocatorLabel == Allocator.Invalid)
            {
                throw new InvalidOperationException("The NativeArray can not be Disposed because it was not allocated with a valid allocator.");
            }

            if (m_Buffer == null)
            {
                throw new InvalidOperationException("The NativeArray is already disposed.");
            }

            if (m_AllocatorLabel > Allocator.None)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                // [DeallocateOnJobCompletion] is not supported, but we want the deallocation
                // to happen in a thread. DisposeSentinel needs to be cleared on main thread.
                // AtomicSafetyHandle can be destroyed after the job was scheduled (Job scheduling
                // will check that no jobs are writing to the container).
                DisposeSentinel.Clear(ref m_DisposeSentinel);

                var jobHandle = new NativeArrayDisposeJob { Data = new NativeArrayDispose { m_Buffer = m_Buffer, m_AllocatorLabel = m_AllocatorLabel, m_Safety = m_Safety } }.Schedule(inputDeps);

                AtomicSafetyHandle.Release(m_Safety);
#else
                var jobHandle = new NativeArrayDisposeJob { Data = new NativeArrayDispose { m_Buffer = m_Buffer, m_AllocatorLabel = m_AllocatorLabel } }.Schedule(inputDeps);
#endif

                m_Buffer = null;
                m_Length = 0;
                m_AllocatorLabel = Allocator.Invalid;

                return jobHandle;
            }

            m_Buffer = null;
            m_Length = 0;

            return inputDeps;
        }

        void Deallocate()
        {
            UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
            m_Buffer = null;
            m_Length = 0;
        }

        [WriteAccessRequired]
        public void CopyFrom(T[] array)
        {
            Copy(array, this);
        }

        [WriteAccessRequired]
        public void CopyFrom(NativeArray<T> array)
        {
            Copy(array, this);
        }

        public void CopyTo(T[] array)
        {
            Copy(this, array);
        }

        public void CopyTo(NativeArray<T> array)
        {
            Copy(this, array);
        }

        public T[] ToArray()
        {
            var array = new T[Length];
            Copy(this, array, Length);
            return array;
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        void FailOutOfRangeError(int index)
        {
            if (index < Length && (m_MinIndex != 0 || m_MaxIndex != Length - 1))
                throw new IndexOutOfRangeException(
                    $"Index {index} is out of restricted IJobParallelFor range [{m_MinIndex}...{m_MaxIndex}] in ReadWriteBuffer.\n" +
                    "ReadWriteBuffers are restricted to only read & write the element at the job index. " +
                    "You can use double buffering strategies to avoid race conditions due to " +
                    "reading & writing in parallel to the same elements from a job.");

            throw new IndexOutOfRangeException($"Index {index} is out of range of '{Length}' Length.");
        }

#endif

        public Enumerator GetEnumerator()
        {
            return new Enumerator(ref this);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new Enumerator(ref this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        [ExcludeFromDocs]
        public struct Enumerator : IEnumerator<T>
        {
            NativeArray<T> m_Array;
            int m_Index;

            public Enumerator(ref NativeArray<T> array)
            {
                m_Array = array;
                m_Index = -1;
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                m_Index++;
                return m_Index < m_Array.Length;
            }

            public void Reset()
            {
                m_Index = -1;
            }

            // Let NativeArray indexer check for out of range.
            public T Current => m_Array[m_Index];

            object IEnumerator.Current => Current;
        }

        public bool Equals(NativeArray<T> other)
        {
            return m_Buffer == other.m_Buffer && m_Length == other.m_Length;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is NativeArray<T> && Equals((NativeArray<T>)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)m_Buffer * 397) ^ m_Length;
            }
        }

        public static bool operator==(NativeArray<T> left, NativeArray<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator!=(NativeArray<T> left, NativeArray<T> right)
        {
            return !left.Equals(right);
        }

        public static void Copy(NativeArray<T> src, NativeArray<T> dst)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);

            if (src.Length != dst.Length)
                throw new ArgumentException("source and destination length must be the same");

#endif
            Copy(src, 0, dst, 0, src.Length);
        }

        public static void Copy(T[] src, NativeArray<T> dst)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);

            if (src.Length != dst.Length)
                throw new ArgumentException("source and destination length must be the same");

#endif
            Copy(src, 0, dst, 0, src.Length);
        }

        public static void Copy(NativeArray<T> src, T[] dst)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);

            if (src.Length != dst.Length)
                throw new ArgumentException("source and destination length must be the same");

#endif
            Copy(src, 0, dst, 0, src.Length);
        }

        public static void Copy(NativeArray<T> src, NativeArray<T> dst, int length)
        {
            Copy(src, 0, dst, 0, length);
        }

        public static void Copy(T[] src, NativeArray<T> dst, int length)
        {
            Copy(src, 0, dst, 0, length);
        }

        public static void Copy(NativeArray<T> src, T[] dst, int length)
        {
            Copy(src, 0, dst, 0, length);
        }

        public static void Copy(NativeArray<T> src, int srcIndex, NativeArray<T> dst, int dstIndex, int length)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "length must be equal or greater than zero.");

            if (srcIndex < 0 || srcIndex > src.Length || (srcIndex == src.Length && src.Length > 0))
                throw new ArgumentOutOfRangeException(nameof(srcIndex), "srcIndex is outside the range of valid indexes for the source NativeArray.");

            if (dstIndex < 0 || dstIndex > dst.Length || (dstIndex == dst.Length && dst.Length > 0))
                throw new ArgumentOutOfRangeException(nameof(dstIndex), "dstIndex is outside the range of valid indexes for the destination NativeArray.");

            if (srcIndex + length > src.Length)
                throw new ArgumentException("length is greater than the number of elements from srcIndex to the end of the source NativeArray.", nameof(length));

            if (dstIndex + length > dst.Length)
                throw new ArgumentException("length is greater than the number of elements from dstIndex to the end of the destination NativeArray.", nameof(length));

#endif
            UnsafeUtility.MemCpy(
                (byte*)dst.m_Buffer + dstIndex * UnsafeUtility.SizeOf<T>(),
                (byte*)src.m_Buffer + srcIndex * UnsafeUtility.SizeOf<T>(),
                length * UnsafeUtility.SizeOf<T>());
        }

        public static void Copy(T[] src, int srcIndex, NativeArray<T> dst, int dstIndex, int length)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);

            if (src == null)
                throw new ArgumentNullException(nameof(src));

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "length must be equal or greater than zero.");

            if (srcIndex < 0 || srcIndex > src.Length || (srcIndex == src.Length && src.Length > 0))
                throw new ArgumentOutOfRangeException(nameof(srcIndex), "srcIndex is outside the range of valid indexes for the source array.");

            if (dstIndex < 0 || dstIndex > dst.Length || (dstIndex == dst.Length && dst.Length > 0))
                throw new ArgumentOutOfRangeException(nameof(dstIndex), "dstIndex is outside the range of valid indexes for the destination NativeArray.");

            if (srcIndex + length > src.Length)
                throw new ArgumentException("length is greater than the number of elements from srcIndex to the end of the source array.", nameof(length));

            if (dstIndex + length > dst.Length)
                throw new ArgumentException("length is greater than the number of elements from dstIndex to the end of the destination NativeArray.", nameof(length));

#endif
            var handle = GCHandle.Alloc(src, GCHandleType.Pinned);
            var addr = handle.AddrOfPinnedObject();

            UnsafeUtility.MemCpy(
                (byte*)dst.m_Buffer + dstIndex * UnsafeUtility.SizeOf<T>(),
                (byte*)addr + srcIndex * UnsafeUtility.SizeOf<T>(),
                length * UnsafeUtility.SizeOf<T>());

            handle.Free();
        }

        public static void Copy(NativeArray<T> src, int srcIndex, T[] dst, int dstIndex, int length)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);

            if (dst == null)
                throw new ArgumentNullException(nameof(dst));

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "length must be equal or greater than zero.");

            if (srcIndex < 0 || srcIndex > src.Length || (srcIndex == src.Length && src.Length > 0))
                throw new ArgumentOutOfRangeException(nameof(srcIndex), "srcIndex is outside the range of valid indexes for the source NativeArray.");

            if (dstIndex < 0 || dstIndex > dst.Length || (dstIndex == dst.Length && dst.Length > 0))
                throw new ArgumentOutOfRangeException(nameof(dstIndex), "dstIndex is outside the range of valid indexes for the destination array.");

            if (srcIndex + length > src.Length)
                throw new ArgumentException("length is greater than the number of elements from srcIndex to the end of the source NativeArray.", nameof(length));

            if (dstIndex + length > dst.Length)
                throw new ArgumentException("length is greater than the number of elements from dstIndex to the end of the destination array.", nameof(length));

#endif
            var handle = GCHandle.Alloc(dst, GCHandleType.Pinned);
            var addr = handle.AddrOfPinnedObject();

            UnsafeUtility.MemCpy(
                (byte*)addr + dstIndex * UnsafeUtility.SizeOf<T>(),
                (byte*)src.m_Buffer + srcIndex * UnsafeUtility.SizeOf<T>(),
                length * UnsafeUtility.SizeOf<T>());

            handle.Free();
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckReinterpretLoadRange<U>(int sourceIndex) where U : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            int tsize = UnsafeUtility.SizeOf<T>();
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);

            int usize = UnsafeUtility.SizeOf<U>();
            long byteSize = Length * tsize;

            long firstByte = sourceIndex * tsize;
            long lastByte = firstByte + usize;

            if (firstByte < 0 || lastByte > byteSize)
                throw new ArgumentOutOfRangeException(nameof(sourceIndex), "loaded byte range must fall inside container bounds");
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckReinterpretStoreRange<U>(int destIndex) where U : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            int tsize = UnsafeUtility.SizeOf<T>();
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);

            int usize = UnsafeUtility.SizeOf<U>();
            long byteSize = Length * tsize;

            long firstByte = destIndex * tsize;
            long lastByte = firstByte + usize;

            if (firstByte < 0 || lastByte > byteSize)
                throw new ArgumentOutOfRangeException(nameof(destIndex), "stored byte range must fall inside container bounds");
#endif
        }

        public U ReinterpretLoad<U>(int sourceIndex) where U : struct
        {
            CheckReinterpretLoadRange<U>(sourceIndex);
            byte* src_ptr = ((byte*)m_Buffer) + UnsafeUtility.SizeOf<T>() * sourceIndex;
            return UnsafeUtility.ReadArrayElement<U>(src_ptr, 0);
        }

        public void ReinterpretStore<U>(int destIndex, U data) where U : struct
        {
            CheckReinterpretStoreRange<U>(destIndex);
            byte* dst_ptr = ((byte*)m_Buffer) + UnsafeUtility.SizeOf<T>() * destIndex;
            UnsafeUtility.WriteArrayElement<U>(dst_ptr, 0, data);
        }

        private NativeArray<U> InternalReinterpret<U>(int length) where U : struct
        {
            var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<U>(m_Buffer, length, m_AllocatorLabel);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, m_Safety);
            SetDisposeSentinel(ref result);
#endif
            return result;
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        // DisposeSentinel is a class so that's not supported in Burst. However, in Burst it's guaranteed
        // that the sentinel is null anyway, so we can just use BurstDiscard on the place that works on it.
        [BurstDiscard]
        void SetDisposeSentinel<U>(ref NativeArray<U> result) where U : struct
        {
            result.m_DisposeSentinel = m_DisposeSentinel;
        }

#endif

        public NativeArray<U> Reinterpret<U>() where U : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (UnsafeUtility.SizeOf<T>() != UnsafeUtility.SizeOf<U>())
            {
                throw new InvalidOperationException($"Types {typeof(T)} and {typeof(U)} are different sizes - direct reinterpretation is not possible. If this is what you intended, use Reinterpret(<type size>)");
            }
#endif
            return InternalReinterpret<U>(Length);
        }

        public NativeArray<U> Reinterpret<U>(int expectedTypeSize) where U : struct
        {
            var tSize = UnsafeUtility.SizeOf<T>();
            var uSize = UnsafeUtility.SizeOf<U>();

            var byteLen = ((long)Length) * tSize;
            var uLen = byteLen / uSize;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (tSize != expectedTypeSize)
            {
                throw new InvalidOperationException($"Type {typeof(T)} was expected to be {expectedTypeSize} but is {tSize} bytes");
            }

            if (uLen * uSize != byteLen)
            {
                throw new InvalidOperationException($"Types {typeof(T)} (array length {Length}) and {typeof(U)} cannot be aliased due to size constraints. The size of the types and lengths involved must line up.");
            }
#endif
            return InternalReinterpret<U>((int)uLen);
        }

        public NativeArray<T> GetSubArray(int start, int length)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (start < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(start), "start must be >= 0");
            }

            if (start + length > Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length), $"sub array range {start}-{start+length-1} is outside the range of the native array 0-{Length-1}");
            }
#endif
            var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(((byte*)m_Buffer) + UnsafeUtility.SizeOf<T>() * start, length, Allocator.Invalid);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, m_Safety);
            result.m_DisposeSentinel = null;
#endif
            return result;
        }

        public ReadOnly AsReadOnly()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new ReadOnly(m_Buffer, m_Length, ref m_Safety);
#else
            return new ReadOnly(m_Buffer, m_Length);
#endif
        }

        [NativeContainer]
        [NativeContainerIsReadOnly]
        public unsafe struct ReadOnly
        {
            [NativeDisableUnsafePtrRestriction]
            internal void* m_Buffer;
            internal int   m_Length;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;

            internal ReadOnly(void* buffer, int length, ref AtomicSafetyHandle safety)
            {
                m_Buffer = buffer;
                m_Length = length;
                m_Safety = safety;
            }

#else
            internal ReadOnly(void* buffer, int length)
            {
                m_Buffer = buffer;
                m_Length = length;
            }

#endif

            public T this[int index]
            {
                get
                {
                    CheckElementReadAccess(index);
                    return UnsafeUtility.ReadArrayElement<T>(m_Buffer, index);
                }
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckElementReadAccess(int index)
            {
                if (index < 0
                    &&  index >= m_Length)
                {
                    throw new IndexOutOfRangeException($"Index {index} is out of range (must be between 0 and {m_Length-1}).");
                }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                int version = m_Safety.UncheckedGetNodeVersion();
                if (m_Safety.version != (version & AtomicSafetyNodeVersionMask.VersionAndReadProtect))
                    AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            }
        }

        // Used by jobs codegen to handle the result of a NativeList.AsDeferredJobArray
        // TODO it sucks that every NativeArray<T> has its own version of this method;
        // but NativeArray<T> might be unmanaged due to DisposeSentinel.  So we can't just
        // overlay a bare struct on it.  We *could* create an UnsafeNativeArray, just like
        // there's UnafeList, that doesn't include the DiposeSentinel.
        unsafe void ResolveDeferredConvertListToArray()
        {
            var bufferAddress = (long) m_Buffer;

            // We use the first bit of the pointer to infer that the array is in list mode
            // Thus the job scheduling code will need to patch it.
            // this &1 contract comes from NativeList.AsDeferredJobArray
            //
            // TODO -- this deferred List to Array logic should really live in UnsafeList.  It can have internals
            // access to NativeArray so it can do this munging, so that all the logic lives in one place.
            if ((bufferAddress & 1) != 0)
            {
                var listData = (Unity.Collections.LowLevel.Unsafe.NativeArrayUnsafeUtility.NativeListData*) (bufferAddress & ~1);

                m_Buffer = listData->buffer;
                m_Length = listData->length;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_MinIndex = 0;
                m_MaxIndex = listData->length - 1;
#endif
            }
        }
    }

    [NativeContainer]
    internal unsafe struct NativeArrayDispose
    {
        [NativeDisableUnsafePtrRestriction]
        internal void*     m_Buffer;
        internal Allocator m_AllocatorLabel;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
#endif

        public void Dispose()
        {
            UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
        }
    }

    // [BurstCompile] - can't use attribute since it's inside com.unity.collections.
    internal struct NativeArrayDisposeJob : IJob
    {
        internal NativeArrayDispose Data;

        public void Execute()
        {
            Data.Dispose();
        }
    }

    /// <summary>
    /// DebuggerTypeProxy for <see cref="NativeArray{T}"/>
    /// </summary>
    internal sealed class NativeArrayDebugView<T> where T : struct
    {
        NativeArray<T> m_Array;

        public NativeArrayDebugView(NativeArray<T> array)
        {
            m_Array = array;
        }

        public T[] Items => m_Array.ToArray();
    }
}

namespace Unity.Collections.LowLevel.Unsafe
{
    public static class NativeArrayUnsafeUtility
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public static AtomicSafetyHandle GetAtomicSafetyHandle<T>(NativeArray<T> array) where T : struct
        {
            return array.m_Safety;
        }

        public static void SetAtomicSafetyHandle<T>(ref NativeArray<T> array, AtomicSafetyHandle safety) where T : struct
        {
            array.m_Safety = safety;
        }

#endif

        /// Internal method used typically by other systems to provide a view on them.
        /// The caller is still the owner of the data.
        public static unsafe NativeArray<T> ConvertExistingDataToNativeArray<T>(void* dataPointer, int length, Allocator allocator) where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be >= 0");

            NativeArray<T>.IsUnmanagedAndThrow();

            var totalSize = UnsafeUtility.SizeOf<T>() * (long)length;
            // Make sure we cannot allocate more than int.MaxValue (2,147,483,647 bytes)
            // because the underlying UnsafeUtility.Malloc is expecting a int.
            // TODO: change UnsafeUtility.Malloc to accept a UIntPtr length instead to match C++ API
            if (totalSize > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(length), $"Length * sizeof(T) cannot exceed {int.MaxValue} bytes");
#endif

            var newArray = new NativeArray<T>
            {
                m_Buffer = dataPointer,
                m_Length = length,
                m_AllocatorLabel = allocator,

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_MinIndex = 0,
                m_MaxIndex = length - 1,
#endif
            };

            return newArray;
        }

        public static unsafe void* GetUnsafePtr<T>(this NativeArray<T> nativeArray) where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(nativeArray.m_Safety);
#endif
            return nativeArray.m_Buffer;
        }

        public static unsafe void* GetUnsafeReadOnlyPtr<T>(this NativeArray<T> nativeArray) where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(nativeArray.m_Safety);
#endif
            return nativeArray.m_Buffer;
        }

        public static unsafe void* GetUnsafeBufferPointerWithoutChecks<T>(NativeArray<T> nativeArray) where T : struct
        {
            return nativeArray.m_Buffer;
        }

        // Used by deferred list to array.  We can't access UnsafeList directly because Unity.ZeroJobs doesn't
        // link to collections.
#pragma warning disable 649
        internal unsafe struct NativeListData
        {
            internal void* buffer;
            internal int length;
            internal int capacity;
        }
#pragma warning restore 649
    }
}
