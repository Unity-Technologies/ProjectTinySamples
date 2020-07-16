namespace Unity.Collections
{
    public enum Allocator
    {
        // NOTE: The items must be kept in sync with Runtime/Export/Collections/NativeCollectionAllocator.h

        Invalid = 0,
        // NOTE: this is important to let Invalid = 0 so that new NativeArray<xxx>() will lead to an invalid allocation by default.

        None = 1,
        Temp = 2,
        TempJob = 3,
        Persistent = 4
    }
}