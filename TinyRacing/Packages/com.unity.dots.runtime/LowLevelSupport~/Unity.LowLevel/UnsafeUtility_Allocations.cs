using System;
using System.Runtime.InteropServices;

namespace Unity.Collections.LowLevel.Unsafe
{
    public partial class UnsafeUtility
    {
        [DllImport("lib_unity_lowlevel", EntryPoint = "unsafeutility_malloc")]
        public static extern unsafe void* Malloc(long totalSize, int alignOf, Allocator allocator);

        [DllImport("lib_unity_lowlevel", EntryPoint = "unsafeutility_memcpy")]
        public static extern unsafe void MemCpy(void* dst, void* src, long n);

        // Debugging. Checks the heap guards on the requested memory.
        [DllImport("lib_unity_lowlevel", EntryPoint = "unsafeutility_assertheap")]
        public static extern unsafe void AssertHeap(void* dst);

        [DllImport("lib_unity_lowlevel", EntryPoint = "unsafeutility_free")]
        public static extern unsafe void Free(void* mBuffer, Allocator mAllocatorLabel);

        [DllImport("lib_unity_lowlevel", EntryPoint = "unsafeutility_memset")]
        public static extern unsafe void MemSet(void* destination, byte value, long size);

        [DllImport("lib_unity_lowlevel", EntryPoint = "unsafeutility_memclear")]
        public static extern unsafe void MemClear(void* mBuffer, long size);

        [DllImport("lib_unity_lowlevel", EntryPoint = "unsafeutility_memcpystride")]
        public static extern unsafe void MemCpyStride(void* destination, int destinationStride, void* source, int sourceStride, int elementSize, long count);

        [DllImport("lib_unity_lowlevel", EntryPoint = "unsafeutility_memcmp")]
        public static extern unsafe int MemCmp(void* ptr1, void* ptr2, long size);

        [DllImport("lib_unity_lowlevel", EntryPoint = "unsafeutility_memcpyreplicate")]
        public static extern unsafe void MemCpyReplicate(void* destination, void* source, int size, int count);

        [DllImport("lib_unity_lowlevel", EntryPoint = "unsafeutility_memmove")]
        public static extern unsafe void MemMove(void* destination, void* source, long size);

        [DllImport("lib_unity_lowlevel", EntryPoint = "unsafeutility_freetemp")]
        public static extern unsafe void FreeTempMemory();

        // The CallFunctionPtr_abc methods are used to call static code-gen methods.
        // If we remove the minimal path for ST, these could be removed.
        [DllImport("lib_unity_lowlevel", EntryPoint = "unsafeutility_call_p")]
        public static extern unsafe void CallFunctionPtr_p(void* fnc, void* data);

        [DllImport("lib_unity_lowlevel", EntryPoint = "unsafeutility_call_pp")]
        public static extern unsafe void CallFunctionPtr_pp(void* fnc, void* data1, void* data2);

        [DllImport("lib_unity_lowlevel", EntryPoint = "unsafeutility_call_pi")]
        public static extern unsafe void CallFunctionPtr_pi(void* fnc, void* data, int param0);

#if UNITY_SINGLETHREADED_JOBS
        // Debugging / testing. Useful to check if the memory deletion that was expected actually did happen.
        // Only reliable & useful when running or testing single threaded.
        [DllImport("lib_unity_lowlevel", EntryPoint = "unsafeutility_get_last_free_ptr")]
        internal static extern unsafe void* GetLastFreePtr();

        // Also debug, ST. Temp never shrinks. TempJob and Persistent return the current heap size.
        // This could work in MT, but the tracking needs to be implemented with a mutex.
        [DllImport("lib_unity_lowlevel", EntryPoint = "unsafeutility_get_heap_size")]
        internal static extern long GetHeapSize(Allocator allocator);

#endif
        // We need a shared pointer with Burst; however, lowlevel doesn't have access to Burst to
        // use the typical machinery. Until (if?) that is solved, we need a workaround to pass
        // a static flag to bursted code.
        [DllImport("lib_unity_lowlevel", EntryPoint = "unsafeutility_get_in_job")]
        internal static extern int GetInJob();

        [DllImport("lib_unity_lowlevel", EntryPoint = "unsafeutility_set_in_job")]
        internal static extern void SetInJob(int inJob);

        public static bool IsValidAllocator(Allocator allocator) { return allocator > Allocator.None; }
    }
}
