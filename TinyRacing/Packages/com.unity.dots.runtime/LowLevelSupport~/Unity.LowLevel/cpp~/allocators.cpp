#include <allocators.h>
#include <baselibext.h>
#include <C/Baselib_Memory.h>

#include "guard.h"
#include "BumpAllocator.h"

#include <stdlib.h>
#include "string.h"

using namespace Unity::LowLevel;

#ifdef GUARD_HEAP
#include <vector>
#endif

#ifdef TRACY_ENABLE
#include "Tracy.hpp"
#endif

#ifdef _WIN32
#define DOEXPORT __declspec(dllexport)
#define CALLEXPORT __stdcall
#else
#define DOEXPORT __attribute__ ((visibility ("default")))
#define CALLEXPORT
#endif

// SSE requires 16 bytes alignment
// AVX 256 is 32 bytes
// AVX 512 is 64 bytes
// Arm tends to be 4 bytes
// Arm NEON can be up to 16 bytes (sometimes 2, 4, or 8 depending on instruction)
#if INTPTR_MAX == INT64_MAX
static const int ARCH_ALIGNMENT = 16;
#elif INTPTR_MAX == INT32_MAX
static const int ARCH_ALIGNMENT = 8;
#else
#error Unknown pointer size or missing size macros!
#endif

static void* lastFreePtr = 0;
static int64_t heapUsage[(int)Allocator::NumAllocators] = {0};   // Debugging, single threaded. (Needs mutex for MT).

#ifdef GUARD_HEAP
static std::vector<void*> sBumpAllocTrack;         // Seperately tracks memory in the bump allocator, so it can be checked with Guards
#endif
static BumpAllocator sBumpAlloc;
static baselib::Lock sBumpAllocMutex;

extern "C" 
{

DOEXPORT
void* CALLEXPORT unsafeutility_malloc(int64_t size, int alignOf, Allocator allocatorType)
{
    // Alignment is a power of 2
    MEM_ASSERT(alignOf == 0 || ((alignOf - 1) & alignOf) == 0);

    // Alignment is not greater than 65536
    MEM_ASSERT(alignOf < 65536);
    
    if (alignOf < ARCH_ALIGNMENT)
        alignOf = ARCH_ALIGNMENT;

#ifdef GUARD_HEAP
    heapUsage[(int)allocatorType] += size;

    int headerSize = alignOf < sizeof(GuardHeader) ? sizeof(GuardHeader) : alignOf;
    int64_t paddedSize = 
        headerSize +                            // Size for the header, or alignOf, whichever is greater.
        size +                                  // Size of user request
        GUARD_PAD;                              // Size of tail buffer (in bytes - no need to align)
#else
    int headerSize = 0;
    int64_t paddedSize = size;
#endif

    void* memBase;
    void* memUser;
    if (allocatorType == Allocator::Temp)
    {
        BaselibLock lock(sBumpAllocMutex);
        memBase = sBumpAlloc.alloc((int)paddedSize, alignOf);
        memUser = (void*)((uint8_t*)memBase + headerSize);

#ifdef GUARD_HEAP
        sBumpAllocTrack.push_back(memUser);
#endif
    }
    else
    {
        memBase = Baselib_Memory_AlignedAllocate(paddedSize, alignOf);
        memUser = (void*)((uint8_t*)memBase + headerSize);

        // Track memory size and pointer
#ifdef TRACY_ENABLE
        TracyAlloc(memUser, size);
#endif
    }

#ifdef GUARD_HEAP
    setupGuardedMemory(memUser, headerSize, size);
#endif

    return memUser;
}

DOEXPORT
void CALLEXPORT unsafeutility_assertheap(void* ptr)
{
    MEM_ASSERT(ptr);
#ifdef GUARD_HEAP
    checkGuardedMemory(ptr, false);
#endif
}

DOEXPORT
void CALLEXPORT unsafeutility_free(void* ptr, Allocator allocatorType)
{
    lastFreePtr = ptr;
    if (ptr == nullptr)
        return;

#ifdef GUARD_HEAP
    checkGuardedMemory(ptr, true);
#endif

    if (allocatorType == Allocator::Temp)
        return;

#ifdef TRACY_ENABLE
    TracyFree(ptr);
#endif

#ifdef GUARD_HEAP
    GuardHeader* head = (GuardHeader*)((uint8_t*)ptr - sizeof(GuardHeader));
    GuardHeader* realPtr = (GuardHeader*)((uint8_t*)ptr - head->offset);
    heapUsage[(int)allocatorType] -= head->size;
#else
    void* realPtr = ptr;
#endif

    Baselib_Memory_AlignedFree(realPtr);
}

DOEXPORT
void* CALLEXPORT unsafeutility_get_last_free_ptr()
{
    // Useful debugging trick - did the resources we except to be deleted by opaque code get deleted?
    // But only reliable single-threaded.
    return lastFreePtr;
}

DOEXPORT
long CALLEXPORT unsafeutility_get_heap_size(Allocator allocatorType)
{
    return (long) heapUsage[(int)allocatorType];
}

DOEXPORT
void CALLEXPORT unsafeutility_memset(void* destination, char value, int64_t size)
{
    memset(destination, value, static_cast<size_t>(size));
}

DOEXPORT
void CALLEXPORT unsafeutility_memclear(void* destination, int64_t size)
{
    memset(destination, 0, static_cast<size_t>(size));
}

DOEXPORT
void CALLEXPORT unsafeutility_freetemp()
{
    BaselibLock lock(sBumpAllocMutex);

#ifdef GUARD_HEAP
    for (void *ptr : sBumpAllocTrack)
        checkGuardedMemory(ptr, true);
    sBumpAllocTrack.clear();
#endif

    sBumpAlloc.reset();
}

#define UNITY_MEMCPY memcpy
typedef uint8_t UInt8;

DOEXPORT
void CALLEXPORT unsafeutility_memcpy(void* destination, void* source, int64_t count)
{
    UNITY_MEMCPY(destination, source, (size_t)count);
}

DOEXPORT
void CALLEXPORT unsafeutility_memcpystride(void* destination_, int destinationStride, void* source_, int sourceStride, int elementSize, int64_t count)
{   
    UInt8* destination = (UInt8*)destination_;
    UInt8* source = (UInt8*)source_;
    if (elementSize == destinationStride && elementSize == sourceStride)
    {
        UNITY_MEMCPY(destination, source, static_cast<size_t>(count) * static_cast<size_t>(elementSize));
    }
    else
    {
        for (int i = 0; i != count; i++)
        {
            UNITY_MEMCPY(destination, source, elementSize);
            destination += destinationStride;
            source += sourceStride;
        }
    }
}

DOEXPORT
int32_t CALLEXPORT unsafeutility_memcmp(void* ptr1, void* ptr2, uint64_t size)
{
    return memcmp(ptr1, ptr2, (size_t)size);
}

DOEXPORT
void CALLEXPORT unsafeutility_memcpyreplicate(void* dst, void* src, int size, int count)
{
    uint8_t* dstbytes = (uint8_t*)dst;
    // TODO something smarter
    for (int i = 0; i < count; ++i)
    {
        memcpy(dstbytes, src, size);
        dstbytes += size;
    }
}

DOEXPORT
void CALLEXPORT unsafeutility_memmove(void* dst, void* src, uint64_t size)
{
    memmove(dst, src, (size_t)size);
}


typedef void (*Call_p)(void*);
typedef void (*Call_pp)(void*, void*);
typedef void (*Call_pi)(void*, int);

DOEXPORT
void CALLEXPORT unsafeutility_call_p(void* f, void* data)
{
    MEM_ASSERT(f);
    Call_p func = (Call_p) f;
    func(data);
}

DOEXPORT
void CALLEXPORT unsafeutility_call_pp(void* f, void* data1, void* data2)
{
    MEM_ASSERT(f);
    Call_pp func = (Call_pp)f;
    func(data1, data2);
}

DOEXPORT
void CALLEXPORT unsafeutility_call_pi(void* f, void* data, int i)
{
    MEM_ASSERT(f);
    Call_pi func = (Call_pi)f;
    func(data, i);
}

int inJob = 0;

DOEXPORT
int CALLEXPORT unsafeutility_get_in_job()
{
    return inJob;
}

DOEXPORT
void CALLEXPORT unsafeutility_set_in_job(int v)
{
    inJob = v;
}

} // extern "C"
