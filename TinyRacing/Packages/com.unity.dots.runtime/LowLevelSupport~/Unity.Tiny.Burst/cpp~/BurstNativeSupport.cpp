#include <Unity/Runtime.h>
#include <map>
#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include <allocators.h>
#include <baselibext.h>
using namespace Unity::LowLevel;

struct Hash128
{
    int64_t p0;
    int64_t p1;

    bool operator< (const Hash128& against) const
    {
        return (p0 < against.p0) || (p0 == against.p0 && p1 < against.p1);
    }
};

struct SharedMemoryInfo
{
    SharedMemoryInfo() : ptr(nullptr), size_of(0), alignment(0) { }

    SharedMemoryInfo(void* ptr, uint32_t size_of, uint32_t alignment) : ptr(ptr), size_of(size_of), alignment(alignment) { }

    void* ptr;
    uint32_t size_of;
    uint32_t alignment;
};

static baselib::Lock _globalSharedMemoryLock;
static std::map<Hash128, SharedMemoryInfo, std::less<Hash128>> SharedMemoryMap;

// This function is a copy of BurstCompilerService::GetOrCreateSharedMemory in Unity Runtime\Burst\Burst.Cpp
//
// This function allows to allocate a permanent memory (that is protected from AppDomain reload)
// that can be accessed from both burst code and regular C# code
// It is expected that for a specified key, the size_of/alignement will be always the same
// Otherwise it will return a null pointer
DOTS_EXPORT(void*) GetOrCreateSharedMemory(const Hash128& key, uint32_t size_of, uint32_t alignment)
{
    if (size_of == 0) 
        return nullptr;

    if (alignment == 0)
        alignment = 4;

    BaselibLock _(_globalSharedMemoryLock);

    // ------------------------------------------------------------------
    // 1) Try to get or create HashMap for contextKey
    // ------------------------------------------------------------------
    SharedMemoryInfo sharedMemInfo;

    void* returnPtr = nullptr;
    std::map<Hash128, SharedMemoryInfo>::iterator subIt = SharedMemoryMap.find(key);

    if (subIt == SharedMemoryMap.end())
    {
        void* ptr = unsafeutility_malloc(size_of, alignment, Allocator::Persistent);
        SharedMemoryMap.insert(std::pair<Hash128, SharedMemoryInfo>(key, SharedMemoryInfo(ptr, size_of, alignment)));

        returnPtr = ptr;
        memset(returnPtr, 0, size_of);
    }
    else
    {
        sharedMemInfo = subIt->second;
        assert(sharedMemInfo.ptr != nullptr);
        // We are disallowing to change the size_of the original shared memory as the pointers
        // cannot be reallocated for already pre-compiled code in burst
        if (size_of != sharedMemInfo.size_of || alignment != sharedMemInfo.alignment)
        {
            // In that case, it is invalid, we return nullptr and we will throw an exception at client call site
            // so that user can see which SharedStatic<T> is failing (and to let this code compatible
            // with burst, as we should not throw a managed exception here)
            returnPtr = nullptr;
        }
        else
        {
            returnPtr = sharedMemInfo.ptr;
        }
    }

    return returnPtr;
}
