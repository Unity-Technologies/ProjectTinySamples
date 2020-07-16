#pragma once

#include <stdint.h>
#include <Unity/Runtime.h>

namespace Unity::LowLevel
{
    enum class Allocator
    {
        // NOTE: The items must be kept in sync with Runtime/Export/Collections/NativeCollectionAllocator.h
        Invalid = 0,
        // NOTE: this is important to let Invalid = 0 so that new NativeArray<xxx>() will lead to an invalid allocation by default.
        None = 1,
        Temp = 2,
        TempJob = 3,
        Persistent = 4,
        NumAllocators = 5
    };

    DOTS_EXPORT(void*) unsafeutility_malloc(int64_t size, int alignment, Allocator allocatorType);
    DOTS_EXPORT(void) unsafeutility_assertheap(void* ptr);
    DOTS_EXPORT(void) unsafeutility_free(void* ptr, Allocator allocatorType);
    DOTS_EXPORT(void*) unsafeutility_get_last_free_ptr();
    DOTS_EXPORT(void) unsafeutility_memset(void* destination, char value, int64_t size);
    DOTS_EXPORT(void) unsafeutility_memclear(void* destination, int64_t size);
    DOTS_EXPORT(void) unsafeutility_freetemp();
    DOTS_EXPORT(void) unsafeutility_memcpy(void* destination, void* source, int64_t count);
    DOTS_EXPORT(void) unsafeutility_memcpystride(void* destination_, int destinationStride, void* source_, int sourceStride, int elementSize, int64_t count);
    DOTS_EXPORT(int32_t) unsafeutility_memcmp(void* ptr1, void* ptr2, uint64_t size);
    DOTS_EXPORT(void) unsafeutility_memcpyreplicate(void* dst, void* src, int size, int count);
    DOTS_EXPORT(void) unsafeutility_memmove(void* dst, void* src, uint64_t size);
    DOTS_EXPORT(void) unsafeutility_call_p(void* f, void* data);
    DOTS_EXPORT(void) unsafeutility_call_pi(void* f, void* data, int i);

} // namespace Unity::LowLevel
