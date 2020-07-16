#if ENABLE_UNITY_COLLECTIONS_CHECKS
using System.Runtime.InteropServices;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Jobs;

namespace Unity.Collections.LowLevel.Unsafe
{
    // AtomicSafetyHandle is used by the C# job system to provide validation and full safety
    // for read / write permissions to access the buffers represented by each handle.
    // Each AtomicSafetyHandle represents a single container struct (or resource).
    // Since all Native containers are written using structs,
    // it also provides checks against destroying a container
    // and accessing from another struct pointing to the same buffer.
    //
    // AtomicSafetyNodes represent the actual state of a valid AtomicSafetyHandle.
    // They are associated with a container's allocated memory buffer. Because Native
    // Containers are copyable structs, there can be multiple copies which point to 
    // the same underlying memory.
    // If they become out-of-sync (tracked by version increments), the AtomicSafetyHandle
    // is invalid. Checking for this is safe and will never be a memory access error because 
    // once allocated, AtomicSafetyNodes live to the end of the application's life. Released
    // AtomicSafetyNodes are recycled via free-list.
    //
    // The key to setting permissions in the AtomicSafetyHandles lies in attributes
    // set for the Native Containers in C#. AtomicSafetyNodes permissions were patched at runtime
    // using reflection in Big Unity, but with DOTS Runtime, they will have to be patched
    // at compile time using IL code generation. Containers also may manually set read/write only.
    //
    // IMPL NOTE 1: One tricky behavior to note is that when we call Check***AndThrow, if the handle doesn't
    // provide access, yet that REASON to throw can't be reasoned about, the handle will unprotect
    // that relevant access and continue execution.
    //
    // AtomicSafetyNodes actually track two version numbers. It allows NativeList cast to NativeArray, so the
    // NativeList can continue to be resized dynamically (which invalidates the version in the NativeArray
    // using the secondary version in the node).
    //
    // IMPL NOTE 2: Another tricky behavior is the presense of AllowSecondaryWriting as well as WriteProtect
    // in the node's flag and secondary version, respectively. The idea is that WriteProtect enforces protection,
    // but AllowSecondaryWriting will keep the CheckWriteAndThrow function from auto-enabling write, as
    // described in IMPL NOTE 1 above. These Check***AndThrow functions are responsible for most of the
    // transitions between setting and unsetting protection to the SafetyHandles and underlying nodes.
    //
    // Differences from CPP impl. in Big Unity
    // - Prepare******BufferFromJob methods are not implemented. They are special-case methods
    //   used in JobsBindings in Big Unity (esp. ExecuteJobWithSharedJobData) which is only
    //   used for some DSP Graph stuff, AudioOutputHookManager, and AnimationScriptPlayable.
    //   They dangerously patch Safety Handles and make assumptions about not needing
    //   access to other members of the node (flags, magic).
    //   The assumption for DOTS RT is that if we eventually become compatible with these
    //   things, they will undergo conversion and be used in a different system structure anyway, as
    //   the DOTS RT Safety Handles are only relevant in builds.
    //   No way to test at the moment.
    // - PrepareUndisposable is not implemented. Not actually used in Big Unity or DOTS.
    // - AllowReadOrWrite flag removed. Not used in Big Unity or DOTS.

    internal struct AtomicSafetyNodeFlags
    {
        internal const uint AllowSecondaryWriting = 1 << 0;
        internal const uint IsInit = 1 << 1;
        internal const uint AllowDispose = 1 << 2;
        internal const uint BumpSecondaryVersionOnScheduleWrite = 1 << 3;
        internal const uint Magic = ((1u << 28) - 1) << 4;
    }

    // Permission flags are guards. If the flag is set, the node is protected from doing
    // that operation. I.e. for read only, Write+Dispose should be set.
    internal struct AtomicSafetyNodeVersionMask
    {
        internal const int ReadProtect = 1 << 0;
        internal const int WriteProtect = 1 << 1;
        internal const int DisposeProtect = 1 << 2;
        internal const int ReadWriteProtect = ReadProtect | WriteProtect;
        internal const int ReadWriteDisposeProtect = ReadProtect | WriteProtect | DisposeProtect;

        internal const int ReadUnprotect = ~ReadProtect;
        internal const int WriteUnprotect = ~WriteProtect;
        internal const int DisposeUnprotect = ~DisposeProtect;
        internal const int ReadWriteUnprotect = ~ReadWriteProtect;
        internal const int ReadWriteDisposeUnprotect = ~ReadWriteDisposeProtect;

        internal const int VersionAndReadProtect = ~(WriteProtect | DisposeProtect);
        internal const int VersionAndWriteProtect = ~(ReadProtect | DisposeProtect);
        internal const int VersionAndDisposeProtect = ~(ReadProtect | WriteProtect);

        internal const int SecondaryVersion = 1 << 3;   // Track here rather than with pointer alignment as in Big Unity

        internal const int VersionInc = 1 << 4;
    }

    public unsafe struct BufferDebugData
    {
        public JobHandle fence;
        public JobReflectionData* reflectionData;

        // @@TODO buffer info...
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct AtomicSafetyNode
    {
        internal int version0;
        internal int version1;
        internal uint flags;

        internal BufferDebugData writer;
        internal BufferDebugData* readers;
        internal int readerCount;

        internal void Init()
        {
            if ((flags & AtomicSafetyNodeFlags.IsInit) == 0)
            {
                version0 = 0;
                version1 = AtomicSafetyNodeVersionMask.SecondaryVersion;
                flags = AtomicSafetyNodeFlags.Magic | AtomicSafetyNodeFlags.IsInit;
            }
            flags |= AtomicSafetyNodeFlags.AllowDispose;
            flags &= ~AtomicSafetyNodeFlags.BumpSecondaryVersionOnScheduleWrite;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AtomicSafetyNodePatched
    {
        internal int version0;
        internal int version1;
        internal uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AtomicSafetyHandle
    {
#if UNITY_WINDOWS
        const string nativejobslib = "nativejobs";
#else
        const string nativejobslib = "libnativejobs";
#endif
        [DllImport(nativejobslib, EntryPoint= "AtomicStack_Push")]
        private static extern unsafe void PushAtomicNode(void* node);

        [DllImport(nativejobslib, EntryPoint= "AtomicStack_Pop")]
        private static extern unsafe void* PopAtomicNode();

        // @@TODO This should return a safety handle for the current bump allocator scope,
        // that should be marked as released when the bump allocator is reset. It should
        // not be a constant. This is a temp solution to avoid a static member. It is being
        // replaced with a frame-scoped system.
        [DllImport(nativejobslib)]
        private static extern unsafe void SetTempMemSafetyHandle(void* node);// from per-frame/bump allocator
        [DllImport(nativejobslib)]
        private static extern unsafe void* GetTempMemSafetyHandle();

        // Needed for com.unity.physics
        [DllImport(nativejobslib)]
        private static extern unsafe void SetTempSliceHandle(void* node);
        [DllImport(nativejobslib)]
        private static extern unsafe void* GetTempSliceHandle();

        internal unsafe AtomicSafetyNode** nodePtrPtr;
        internal unsafe AtomicSafetyNode* nodePtr;
        internal int version;

        // This is used in a job instead of the shared node, since different jobs may enforce
        // different access to memory/object protected by the safety handle, and once we have
        // verified the job can safely access it without race conditions etc., it should maintain
        // it's own copy of required permissions in that moment for checking with actual code
        // which accesses that memory/object.
        internal unsafe AtomicSafetyNodePatched *nodeLocalPtr;


        //---------------------------------------------------------------------------------------------------
        // Basic lifetime management
        //---------------------------------------------------------------------------------------------------

        public static void Initialize()
        {
            unsafe
            {
                // Keep from initializing twice
                if (GetTempSliceHandle() != null)
                    return;
                var tempSliceHandle = CreateOnHeap();
                SetTempSliceHandle(tempSliceHandle);
                tempSliceHandle->SetAllowSecondaryVersionWriting(false);

                var tempMemSafetyHandle = CreateOnHeap();
                SetTempMemSafetyHandle(tempMemSafetyHandle);
            }
        }

        public unsafe static void Shutdown()
        {
            // Protect from multiple shutdown
            if (GetTempSliceHandle() == null)
                return;
            Release(GetTempUnsafePtrSliceHandle());
            UnsafeUtility.Free(GetTempSliceHandle(), Allocator.Persistent);
            SetTempSliceHandle(null);
            Release(GetTempMemoryHandle());
            UnsafeUtility.Free(GetTempMemSafetyHandle(), Allocator.Persistent);
            SetTempMemSafetyHandle(null);
        }

        public static AtomicSafetyHandle GetTempUnsafePtrSliceHandle()
        {
            unsafe
            {
                return *(AtomicSafetyHandle*) GetTempSliceHandle();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AtomicSafetyHandle GetTempMemoryHandle()
        {
            unsafe
            {
                return *(AtomicSafetyHandle*)GetTempMemSafetyHandle();
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsTempMemoryHandle(AtomicSafetyHandle handle)
        {
            unsafe
            {
                return handle.nodePtrPtr == GetTempMemoryHandle().nodePtrPtr;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void InitHandle(ref AtomicSafetyHandle handle)
        {
            AtomicSafetyNode** nodePtrPtr;
            AtomicSafetyNode* nodePtr;

            nodePtrPtr = (AtomicSafetyNode**)PopAtomicNode();
            if (*nodePtrPtr == null)
            {
                *nodePtrPtr = (AtomicSafetyNode*)UnsafeUtility.Malloc(sizeof(AtomicSafetyNode), 0, Allocator.Persistent);
                UnsafeUtility.MemClear(*nodePtrPtr, sizeof(AtomicSafetyNode));
            }
            nodePtr = *nodePtrPtr;
            nodePtr->Init();

            handle.nodePtrPtr = nodePtrPtr;
            handle.nodePtr = nodePtr;
            handle.version = nodePtr->version0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AtomicSafetyHandle Create()
        {
            AtomicSafetyHandle handle = new AtomicSafetyHandle();
            InitHandle(ref handle);
            return handle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static AtomicSafetyHandle* CreateOnHeap()
        {
            var handlePtr = (AtomicSafetyHandle*)UnsafeUtility.Malloc(sizeof(AtomicSafetyHandle), 0, Allocator.Persistent);
            InitHandle(ref *handlePtr);
            return handlePtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Release(AtomicSafetyHandle handle)
        {
            unsafe
            {
                // Can throw if corrupted or unallowed job
                // Otherwise return null if released already (based on version mismatch), where we will throw
                AtomicSafetyNode* node = handle.GetInternalNode();
                if (node == null)
                    throw new System.InvalidOperationException("The Handle has already been released");

                // Clear all protections and increment version to protect from any other remaining AtomicSafetyHandles
                node->version0 = (node->version0 & AtomicSafetyNodeVersionMask.ReadWriteDisposeUnprotect) + AtomicSafetyNodeVersionMask.VersionInc;
                node->version1 = (node->version1 & AtomicSafetyNodeVersionMask.ReadWriteDisposeUnprotect) + AtomicSafetyNodeVersionMask.VersionInc;
                PushAtomicNode(handle.nodePtrPtr);
            }
        }


        //---------------------------------------------------------------------------------------------------
        // Quick tests (often used to avoid executing much slower test code)
        //---------------------------------------------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe bool IsValid() => (nodePtr != null) &&
            (version == (UncheckedGetNodeVersion() & AtomicSafetyNodeVersionMask.ReadWriteDisposeUnprotect));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool IsAllowedToWrite() => (nodePtr != null) &&
            (version == (UncheckedGetNodeVersion() & AtomicSafetyNodeVersionMask.VersionAndWriteProtect));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool IsAllowedToRead() => (nodePtr != null) &&
            (version == (UncheckedGetNodeVersion() & AtomicSafetyNodeVersionMask.VersionAndReadProtect));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool IsAllowedToDispose() => (nodePtr != null) &&
            (version == (UncheckedGetNodeVersion() & AtomicSafetyNodeVersionMask.VersionAndDisposeProtect));


        //---------------------------------------------------------------------------------------------------
        // Externally used by owners of safety handles to setup safety handles
        //---------------------------------------------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int UncheckedGetNodeVersion() => 
            (version & AtomicSafetyNodeVersionMask.SecondaryVersion) == AtomicSafetyNodeVersionMask.SecondaryVersion ? 
            nodePtr->version1 : nodePtr->version0;

        // Switches the AtomicSafetyHandle to the secondary version number
        // Also clears protections
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void UncheckedUseSecondaryVersion()
        {
            if (UncheckedIsSecondaryVersion())
                throw new System.InvalidOperationException("Already using secondary version");
            version = nodePtr->version1 & AtomicSafetyNodeVersionMask.ReadWriteDisposeUnprotect;
        }
        public static unsafe void UseSecondaryVersion(ref AtomicSafetyHandle handle)
        {
            handle.UncheckedUseSecondaryVersion();
        }

        // Sets whether the secondary version is readonly (allowWriting = false) or readwrite (allowWriting= true)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAllowSecondaryVersionWriting(bool allowWriting)
        {
            unsafe
            {
                var node = GetInternalNode();
                if (node == null)
                    throw new System.InvalidOperationException("Node is not valid in SetAllowSecondaryVersionWriting");

                // This logic is not obvious. For explanation, see comments at top of file.
                node->version1 |= AtomicSafetyNodeVersionMask.WriteProtect;
                if (allowWriting)
                    node->flags |= AtomicSafetyNodeFlags.AllowSecondaryWriting;
                else
                    node->flags &= ~AtomicSafetyNodeFlags.AllowSecondaryWriting;
            }
        }
        public static void SetAllowSecondaryVersionWriting(AtomicSafetyHandle handle, bool allowWriting)
        {
            handle.SetAllowSecondaryVersionWriting(allowWriting);
        }

        // Sets whether the secondary version is readonly (allowWriting = false) or readwrite (allowWriting= true)
        // "bump" means increment.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBumpSecondaryVersionOnScheduleWrite(bool bump)
        {
            unsafe
            {
                var node = GetInternalNode();
                if (node == null)
                    throw new System.InvalidOperationException("Node is not valid in SetBumpSecondaryVersionOnScheduleWrite");
                if (bump)
                    node->flags |= AtomicSafetyNodeFlags.BumpSecondaryVersionOnScheduleWrite;
                else
                    node->flags &= ~AtomicSafetyNodeFlags.BumpSecondaryVersionOnScheduleWrite;
            }
        }
        public static void SetBumpSecondaryVersionOnScheduleWrite(AtomicSafetyHandle handle, bool bump)
        {
            handle.SetBumpSecondaryVersionOnScheduleWrite(bump);
        }


        //---------------------------------------------------------------------------------------------------
        // Used by CodeGen specifically
        //---------------------------------------------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PatchLocal(ref AtomicSafetyHandle handle)
        {
            // TODO fix the temp memory handles
            if (IsTempMemoryHandle(handle)) 
                return;

            unsafe
            {
                if (handle.nodePtr == null)
                    return;

                if (handle.nodeLocalPtr != null)
                    throw new Exception("Code-gen created a duplicate PatchLocal. This is bug.");

                handle.nodeLocalPtr = (AtomicSafetyNodePatched*)UnsafeUtility.Malloc(sizeof(AtomicSafetyNodePatched), 16, Allocator.TempJob);
                *handle.nodeLocalPtr = *(AtomicSafetyNodePatched *)handle.nodePtr;
                
                // Clear bits marking this as a real AtomicSafetyNode
                handle.nodeLocalPtr->flags ^= AtomicSafetyNodeFlags.Magic;
                
                handle.nodePtr = (AtomicSafetyNode*)handle.nodeLocalPtr;
                handle.nodePtrPtr = null;

                handle.version = handle.UncheckedGetNodeVersion() & AtomicSafetyNodeVersionMask.ReadWriteDisposeUnprotect;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UnpatchLocal(ref AtomicSafetyHandle handle)
        {
            // TODO fix the temp memory handles
            if (IsTempMemoryHandle(handle))
                return;

            unsafe
            {
                if (handle.version == -1)
                    throw new Exception("Code-gen created a duplicate UnpatchLocal. This is bug.");

                if (handle.nodeLocalPtr == null)
                    return;

                if ((handle.nodeLocalPtr->flags & AtomicSafetyNodeFlags.Magic) != 0)
                    throw new Exception("UnpatchLocal called, but safety handle was never patched with PatchLocal! This is a codegen bug.");

                UnsafeUtility.Free(handle.nodeLocalPtr, Allocator.TempJob);
                handle.nodeLocalPtr = null;
                handle.nodePtr = null;
                handle.version = -1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetAllowWriteOnly(ref AtomicSafetyHandle handle)
        {
            // TODO fix the temp memory handles
            if (IsTempMemoryHandle(handle)) return;

            unsafe
            {
                handle.nodePtr->version0 = (handle.nodePtr->version0 & AtomicSafetyNodeVersionMask.WriteUnprotect) | AtomicSafetyNodeVersionMask.ReadProtect;
                handle.nodePtr->version1 = (handle.nodePtr->version1 & AtomicSafetyNodeVersionMask.WriteUnprotect) | AtomicSafetyNodeVersionMask.ReadProtect;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetAllowReadOnly(ref AtomicSafetyHandle handle)
        {
            // TODO fix the temp memory handles
            if (IsTempMemoryHandle(handle)) return;

            unsafe
            {
                handle.nodePtr->version0 = (handle.nodePtr->version0 & AtomicSafetyNodeVersionMask.ReadUnprotect) | AtomicSafetyNodeVersionMask.WriteProtect;
                handle.nodePtr->version1 = (handle.nodePtr->version1 & AtomicSafetyNodeVersionMask.ReadUnprotect) | AtomicSafetyNodeVersionMask.WriteProtect;
            }
        }


        //---------------------------------------------------------------------------------------------------
        // JobsDebugger safety checks usage (may be used internally as well)
        //---------------------------------------------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe AtomicSafetyNode* GetInternalNode()
        {
            if (!IsValid())
                return null;
            if ((nodePtr->flags & AtomicSafetyNodeFlags.Magic) == AtomicSafetyNodeFlags.Magic)
                return nodePtr;
            throw new System.InvalidOperationException("AtomicSafetyNode has either been corrupted or is being accessed on a job which is not allowed");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool IsDefaultValue() => version == 0 && nodePtr == null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool UncheckedIsSecondaryVersion() =>
            (version & AtomicSafetyNodeVersionMask.SecondaryVersion) == AtomicSafetyNodeVersionMask.SecondaryVersion;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool ComparePointingToSameBuffer(ref AtomicSafetyHandle other) => GetInternalNode() == other.GetInternalNode();

        public unsafe int GetReaderArray(int maxCount, JobHandle* handles)
        {
            AtomicSafetyNode* node = GetInternalNode();
            if (node == null)
                return 0;

            int count = node->readerCount < maxCount ? node->readerCount : maxCount;
            for (int i = 0; i < count; i++)
                handles[i] = node->readers[i].fence;

            return node->readerCount;
        }
        public unsafe static int GetReaderArray(AtomicSafetyHandle handle, int maxCount, IntPtr handles)
        {
            return handle.GetReaderArray(maxCount, (JobHandle*) handles);
        }

        public JobHandle GetWriter()
        {
            unsafe
            {
                AtomicSafetyNode* node = GetInternalNode();
                if (node != null)
                    return node->writer.fence;
            }
            return new JobHandle();
        }
        public static JobHandle GetWriter(AtomicSafetyHandle handle)
        {
            return handle.GetWriter();
        }

        public static string GetReaderName(AtomicSafetyHandle handle, int readerIndex) => "(GetReaderName not implemented yet)";

        public static string GetWriterName(AtomicSafetyHandle handle) => "(GetWriterName not implemented yet)";


        //---------------------------------------------------------------------------------------------------
        // Should be in JobsDebugger namespace or something because they know both control jobs and safety handles
        //---------------------------------------------------------------------------------------------------

        public static EnforceJobResult EnforceAllBufferJobsHaveCompleted(AtomicSafetyHandle handle) =>
            EnforceJobResult.AllJobsAlreadySynced;

        public static EnforceJobResult EnforceAllBufferJobsHaveCompletedAndRelease(AtomicSafetyHandle handle) =>
            EnforceJobResult.AllJobsAlreadySynced;

        public static EnforceJobResult
            EnforceAllBufferJobsHaveCompletedAndDisableReadWrite(AtomicSafetyHandle handle) =>
                EnforceJobResult.AllJobsAlreadySynced;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckWriteAndThrow(AtomicSafetyHandle handle)
        {
            if (handle.IsAllowedToWrite())
                return;

            if (Unity.Jobs.LowLevel.Unsafe.JobsUtility.IsExecutingJob())
                throw new System.InvalidOperationException("You are not allowed to write this native container or resource");

            unsafe
            {
                AtomicSafetyNode* node = handle.GetInternalNode();
                if (node == null)
                    throw new System.InvalidOperationException("The safety handle is no longer valid -- a native container or other protected resource has been deallocated");

                // @@TODO Need native support
                //If !main thread
                //throw "Native container or resource being used from thread which is not main or belonging to job"

                // @@TODO Need native support
                //if (!CheckDidSyncFence(node->writer.fence))
                //{
                //    Assert(!AtomicSafetyHandle::ValidateIsAllowedToWriteEarlyOut(buffer));
                //    return ReturnErrorMsg(kNativeArrayWriteMainThreadAgainstWriteJob, node->writer, errorMsg);
                //}

                // @@TODO Need code IL post processing
                //for (size_t i = 0; i != node->readers.size(); i++)
                //{
                //    // Only allowed to access data if someone called sync fence on the job or on a job that depends on it.
                //    if (!CheckDidSyncFence(node->readers[i].fence))
                //    {
                //        Assert(!AtomicSafetyHandle::ValidateIsAllowedToWriteEarlyOut(buffer));
                //        return ReturnErrorMsg(kNativeArrayWriteMainThreadAgainstReadJob, node->readers[i], errorMsg);
                //    }
                //}

                if ((node->flags & AtomicSafetyNodeFlags.AllowSecondaryWriting) == 0 && handle.UncheckedIsSecondaryVersion())
                    throw new System.InvalidOperationException("Native container has been declared [ReadOnly] but you are attemping to write to it");

                // If we are write protected, but are no longer in a job and no other safety checks failed, we can remove write protection
                node->version0 &= AtomicSafetyNodeVersionMask.WriteUnprotect;
                if ((node->flags & AtomicSafetyNodeFlags.AllowSecondaryWriting) == AtomicSafetyNodeFlags.AllowSecondaryWriting)
                    node->version1 &= AtomicSafetyNodeVersionMask.WriteUnprotect;
            }

            UnityEngine.Assertions.Assert.IsTrue(handle.IsAllowedToWrite());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckReadAndThrow(AtomicSafetyHandle handle)
        {
            if (handle.IsAllowedToRead())
                return;

            if (Unity.Jobs.LowLevel.Unsafe.JobsUtility.IsExecutingJob())
                throw new System.InvalidOperationException("You are not allowed to read this native container or resource");

            unsafe
            {
                AtomicSafetyNode* node = handle.GetInternalNode();
                if (node == null)
                    throw new System.InvalidOperationException("The safety handle is no longer valid -- a native container or other protected resource has been deallocated");

                // @@TODO Need native support
                //If !main thread
                //throw "Native container or resource being used from thread which is not main or belonging to job"

                // @@TODO Need native support
                //if (!CheckDidSyncFence(node->writer.fence))
                //{
                //    Assert(!AtomicSafetyHandle::ValidateIsAllowedToReadEarlyOut(buffer));
                //    return ReturnErrorMsg(kNativeArrayReadMainThreadAgainstWriteJob, node->writer, errorMsg);
                //}

                // If we are read protected, but are no longer in a job and no other safety checks failed, we can remove read protection
                node->version0 &= AtomicSafetyNodeVersionMask.ReadUnprotect;
                node->version1 &= AtomicSafetyNodeVersionMask.ReadUnprotect;
            }

            UnityEngine.Assertions.Assert.IsTrue(handle.IsAllowedToRead());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckDisposeAndThrow(AtomicSafetyHandle handle)
        {
            if (handle.IsAllowedToDispose())
                return;

            if (Unity.Jobs.LowLevel.Unsafe.JobsUtility.IsExecutingJob())
                throw new System.InvalidOperationException("You are not allowed to Dispose this native container or resource");

            unsafe
            {
                AtomicSafetyNode* node = handle.GetInternalNode();
                if (node == null)
                    throw new System.InvalidOperationException("The safety handle is no longer valid -- a native container or other protected resource has been deallocated");

                // @@TODO Need native support
                //If !main thread
                //throw "Native container or resource being used from thread which is not main or belonging to job"

                // @@TODO Need native support
                //if (!CheckDidSyncFence(node->writer.fence))
                //{
                //    Assert(!AtomicSafetyHandle::ValidateIsAllowedToReadEarlyOut(buffer));
                //    return ReturnErrorMsg(kNativeArrayDeallocateAgainstWriteJob, node->writer, errorMsg);
                //}

                if ((node->flags & AtomicSafetyNodeFlags.AllowDispose) == 0)
                    throw new System.InvalidOperationException("You are not allowed to Dispose this native container or resource");

                // @@TODO Need code IL post processing
                //for (size_t i = 0; i != node->readers.size(); i++)
                //{
                //    // Only allowed to access data if someone called sync fence on the job or on a job that depends on it.
                //    if (!CheckDidSyncFence(node->readers[i].fence))
                //        return ReturnErrorMsg(kNativeArrayDeallocateAgainstReadJob, node->readers[i], errorMsg);
                //}

                // If we are dispose protected, but are no longer in a job and no other safety checks failed, we can remove dispose protection
                node->version0 &= AtomicSafetyNodeVersionMask.DisposeUnprotect;
                node->version1 &= AtomicSafetyNodeVersionMask.DisposeUnprotect;
            }

            UnityEngine.Assertions.Assert.IsTrue(handle.IsAllowedToDispose());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckDeallocateAndThrow(AtomicSafetyHandle handle)
        {
            CheckDisposeAndThrow(handle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckGetSecondaryDataPointerAndThrow(AtomicSafetyHandle handle)
        {
            if (handle.IsAllowedToRead())
                return;

            if (Unity.Jobs.LowLevel.Unsafe.JobsUtility.IsExecutingJob())
                throw new System.InvalidOperationException("You are not allowed to read this native container or resource");

            unsafe
            {
                AtomicSafetyNode* node = handle.GetInternalNode();
                if (node == null)
                    throw new System.InvalidOperationException("The safety handle is no longer valid -- a native container or other protected resource has been deallocated");

                UnityEngine.Assertions.Assert.IsFalse(handle.UncheckedIsSecondaryVersion());

                // @@TODO Need native support
                //If !main thread
                //throw "Native container or resource being used from thread which is not main or belonging to job"

                // @@TODO Need code IL post processing
                // The primary buffer might (List) might resize
                // The secondary buffer does not resize (Array)
                // Thus if it was scheduled as a secondary buffer, we can safely access it
                //if (node->writer.wasScheduledWithSecondaryBuffer == 1)
                //    return;

                // @@TODO Need native support
                //if (!CheckDidSyncFence(node->writer.fence))
                //return;
            }

            UnityEngine.Assertions.Assert.IsFalse(handle.IsAllowedToRead());
            throw new System.InvalidOperationException("The previously scheduled job writes to the NativeList. You must call JobHandle.Complete() on the job before you can cast the NativeList to a NativeArray safely or use NativeList.AsDeferredJobArray() to cast the array when the job executes.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckWriteAndBumpSecondaryVersion(AtomicSafetyHandle handle)
        {
            UnityEngine.Assertions.Assert.IsTrue(handle.IsValid());
            UnityEngine.Assertions.Assert.IsFalse(handle.UncheckedIsSecondaryVersion());

            if (!handle.IsAllowedToWrite())
                CheckWriteAndThrow(handle);
            unsafe
            {
                handle.nodePtr->version1 += AtomicSafetyNodeVersionMask.VersionInc;
                UnityEngine.Assertions.Assert.IsTrue((handle.nodePtr->version0 & AtomicSafetyNodeVersionMask.ReadWriteProtect) == (handle.nodePtr->version1 & AtomicSafetyNodeVersionMask.ReadWriteProtect));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckExistsAndThrow(AtomicSafetyHandle handle)
        {
            if (!handle.IsValid())
                throw new System.InvalidOperationException("The safety handle is no longer valid -- a native container or other protected resource has been deallocated");
        }
    }



    public enum EnforceJobResult
    {
        AllJobsAlreadySynced = 0,
        DidSyncRunningJobs = 1,
        HandleWasAlreadyDeallocated = 2,
    }

    public unsafe struct JobReflectionData
    {
    }
}

#endif // ENABLE_UNITY_COLLECTIONS_CHECKS
