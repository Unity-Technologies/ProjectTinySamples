#if ENABLE_PROFILER

using System;
using System.Runtime.InteropServices;
using UnityEngine.Networking.PlayerConnection;
using static System.Text.Encoding;
using static Unity.Baselib.LowLevel.Binding;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Development.PlayerConnection;
using Unity.Collections;
using UnityEngine.Assertions;

namespace Unity.Development
{
    // unity\Runtime\Profiler\Profiler.h
    public enum ProfilerModes : int
    {
        ProfileDisabled = 0,
        ProfileCPU = 1 << 0,
        ProfileGPU = 1 << 1,
        ProfileRendering = 1 << 2,
        ProfileMemory = 1 << 3,
        ProfileAudio = 1 << 4,
        ProfileVideo = 1 << 5,
        ProfilePhysics = 1 << 6,
        ProfilePhysics2D = 1 << 7,
        ProfileNetworkMessages = 1 << 8,
        ProfileNetworkOperations = 1 << 9,
        ProfileUI = 1 << 10,
        ProfileUIDetails = 1 << 11,
        ProfileGlobalIllumination = 1 << 12,
        ProfileCount = 13,
        ProfileAll = (1 << ProfileCount) - 1
    };

    internal enum ProfilerMemoryRecordMode : int
    {
        RecordNone = 0,
        RecordManagedCallstack = 1,
        RecordAllCallstackFast = 2,
        RecordAllCallstackFull = 3,
    };

    public class PlayerConnectionProfiler
    {
        private static bool init = false;
        private static ProfilerMemoryRecordMode memoryRecordMode = ProfilerMemoryRecordMode.RecordNone;
        private static unsafe MessageStreamBuilder *streamSession;

        public static bool Enabled => Mode != ProfilerModes.ProfileDisabled;
        public unsafe static ProfilerModes Mode => mode.UnsafeDataPointer == null ? ProfilerModes.ProfileDisabled : mode.Data;
        public static readonly SharedStatic<ProfilerModes> mode = SharedStatic<ProfilerModes>.GetOrCreate<PlayerConnectionProfiler, ProfilerModes>();

        public static unsafe void Initialize()
        {
            if (init)
                return;

            mode.Data = ProfilerModes.ProfileDisabled;

            streamSession = MessageStreamManager.CreateBufferSend();

            Connection.RegisterMessage(EditorMessageIds.kProfilerSetMode, (MessageEventArgs a) =>
            {
                fixed (byte* d = a.data)
                {
                    mode.Data = (ProfilerModes)(*(int*)d);
                }
                if (Enabled)
                {
                    SendProfilingCapabilityMessage();
                    ProfilerProtocolSession.SendProfilingSessionInfo();
                }
            });

            Connection.RegisterMessage(EditorMessageIds.kProfilerSetMemoryRecordMode, (MessageEventArgs a) =>
            {
                fixed (byte* d = a.data)
                {
                    memoryRecordMode = (ProfilerMemoryRecordMode)(*(int*)d);
                }
            });

            init = true;
        }

        public static unsafe void Shutdown()
        {
            mode.Data = ProfilerModes.ProfileDisabled;

            MessageStreamManager.DestroyBufferSend(streamSession);

            init = false;
        }

        private static unsafe void SendProfilingCapabilityMessage()
        {
            streamSession->MessageBegin(EditorMessageIds.kProfilerPlayerInfoMessage);
            streamSession->WriteData<uint>(1);  // version - ONLY supported value and it must be this value
            streamSession->WriteData<byte>(0);  // is deep profiling supported
            streamSession->WriteData<byte>(0);  // is deep profiler enabled
            streamSession->WriteData<byte>(0);  // is memory allocation callstack supported
            streamSession->MessageEnd();
        }
    }

    // unity\Modules\Profiler\Runtime\Protocol.h
    internal enum ProfilerMessageType : ushort
    {
        // GLOBAL DATA
        ProfilerState = 0,      // Profiler state change: enabled/disabled, mode, etc.
        MarkerInfo = 1,        // Marker information - name, metadata layout
        //Callstack = 3,          // void* to function name
        //AllProfilerStats = 4,   // Profiler stats (written on a main thread)
        //AudioInstanceData = 5,  // Combined audio stats
        //UISystemCanvas = 6,     // UI stats
        //UIEvents = 7,
        //MethodJitInfo = 8,
        GlobalMessagesCount = 32,

        // THREAD SPECIFIC DATA
        ThreadInfo = GlobalMessagesCount + 1,   // Thread name and id
        Frame = GlobalMessagesCount + 2,        // Frame boundary
        BeginSample = GlobalMessagesCount + 4,
        EndSample = GlobalMessagesCount + 5,
        Sample = GlobalMessagesCount + 6,
        //BeginSampleWithInstanceID = GlobalMessagesCount + 7,
        //SampleWithInstanceID = GlobalMessagesCount + 9,
        BeginSampleWithMetadata = GlobalMessagesCount + 10,
        EndSampleWithMetadata = GlobalMessagesCount + 11,
        SampleWithMetadata = GlobalMessagesCount + 12,
        //GCAlloc = GlobalMessagesCount + 20,

        // ASYNC DATA
        //LocalAsyncMetadataAnchor = GlobalMessagesCount + 21,  // Thread local async metadata anchor (e.g. GPUSamples generated on render thread.)
        //LocalAsyncMetadata = GlobalMessagesCount + 22,
        //LocalGPUSample = GlobalMessagesCount + 23,
        //CleanupThread = GlobalMessagesCount + 24,
        //FlowEvent = GlobalMessagesCount + 25,
    };

    [StructLayout(LayoutKind.Sequential)]
    internal struct ProfilerProtocolStream
    {
        internal const uint kPacketBlockHeader = 0xB10C7EAD;
        internal const uint kPacketBlockFooter = 0xB10CF007;

        internal ulong threadId;

        internal uint blockIndex;
        internal int blockSizeExpected;
        internal int blockSizeStart;

        internal unsafe MessageStreamBuilder *buffer;

        // @@todo also store thread name?

        internal bool init;

        internal void Initialize(IntPtr streamId)
        {
            if (init)
                return;
            threadId = (ulong)streamId;
            blockIndex = 0;
            blockSizeStart = 0;
            blockSizeExpected = 0;
            unsafe
            {
                buffer = MessageStreamManager.CreateBufferSend();
            }
            init = true;
        }

        internal unsafe void Shutdown()
        {
            if (!init)
                return;
            MessageStreamManager.DestroyBufferSend(buffer);
            init = false;
        }

        internal unsafe void EmitBlockBegin(ProfilerMessageType type, int byteSize)
        {
            // message type and 0 padded 32 bit alignment are part of block size
            byteSize += 4;
            blockSizeExpected = byteSize;

            // Block end data will start with a 32 bit value which must be aligned, so consider that when
            // expressing size of the block data
            while ((byteSize & 3) != 0)
                byteSize++;
            buffer->WriteData<uint>(kPacketBlockHeader);
            buffer->WriteData<uint>(blockIndex);
            buffer->WriteData<ulong>(threadId);
            buffer->WriteData<int>(byteSize);
            blockSizeStart = buffer->DataToSendBytes;
            buffer->WriteData<uint>((uint)type);
        }

        internal unsafe void EmitBlockEnd()
        {
            Assert.AreEqual(blockSizeExpected, buffer->DataToSendBytes - blockSizeStart);
            
            // The block end message is validated by containing the potential NEXT block's index
            blockIndex++;
            buffer->WriteData<uint>(blockIndex);
            buffer->WriteData<uint>(kPacketBlockFooter);
        }

        internal unsafe void EmitSample(ProfilerMessageType type, uint markerId, ulong sysTicks, byte metadataCount = 0)
        {
            int bytesData = 16;
            if (metadataCount > 0)
                bytesData += 4;

            EmitBlockBegin(type, bytesData);

            buffer->WriteData<uint>(0);  // flag (1 for mono sample)
            buffer->WriteData<uint>(markerId);
            buffer->WriteData<ulong>(sysTicks);
            if (metadataCount > 0)
                buffer->WriteData<uint>(metadataCount);

            EmitBlockEnd();
        }
    }

    public class ProfilerProtocolThread
    {
        static private readonly SharedStatic<ProfilerProtocolStream> streamThreadLocal = SharedStatic<ProfilerProtocolStream>.GetOrCreate<ProfilerProtocolSession, ProfilerProtocolStream>();

        static internal ref ProfilerProtocolStream Stream
        {
            get
            {
                if (!streamThreadLocal.Data.init)
                    streamThreadLocal.Data.Initialize(Baselib_Thread_GetCurrentThreadId());
                return ref streamThreadLocal.Data;
            }
        }

        static public void CleanAllThreads()
        {
            // @@todo Each thread should register itself so it can be cleaned up (init = false)
            streamThreadLocal.Data.Shutdown();
        }

        // Threadsafe
        static public unsafe void SendBeginSample(uint markerId, ulong sysTicks)
        {
            if (!PlayerConnectionProfiler.Enabled)
                return;

            Stream.buffer->MessageBegin(EditorMessageIds.kProfilerDataMessage);
            Stream.EmitSample(ProfilerMessageType.BeginSample, markerId, sysTicks);
            Stream.buffer->MessageEnd();
        }

        // Threadsafe
        static public unsafe void SendSample(uint markerId, ulong sysTicks)
        {
            if (!PlayerConnectionProfiler.Enabled)
                return;

            Stream.buffer->MessageBegin(EditorMessageIds.kProfilerDataMessage);
            Stream.EmitSample(ProfilerMessageType.Sample, markerId, sysTicks);
            Stream.buffer->MessageEnd();
        }

        // Threadsafe
        static public unsafe void SendEndSample(uint markerId, ulong sysTicks)
        {
            if (!PlayerConnectionProfiler.Enabled)
                return;

            Stream.buffer->MessageBegin(EditorMessageIds.kProfilerDataMessage);
            Stream.EmitSample(ProfilerMessageType.EndSample, markerId, sysTicks);
            Stream.buffer->MessageEnd();
        }

        static public unsafe void SendBeginSampleWithMetadata(uint markerId, ulong sysTicks, byte metadataCount)
        {
            if (!PlayerConnectionProfiler.Enabled)
                return;

            Stream.buffer->MessageBegin(EditorMessageIds.kProfilerDataMessage);
            Stream.EmitSample(ProfilerMessageType.BeginSampleWithMetadata, markerId, sysTicks, metadataCount);
            Stream.buffer->MessageEnd();
        }

        static public unsafe void SendSampleWithMetadata(uint markerId, ulong sysTicks, byte metadataCount)
        {
            if (!PlayerConnectionProfiler.Enabled)
                return;

            Stream.buffer->MessageBegin(EditorMessageIds.kProfilerDataMessage);
            Stream.EmitSample(ProfilerMessageType.SampleWithMetadata, markerId, sysTicks, metadataCount);
            Stream.buffer->MessageEnd();
        }

        static public unsafe void SendEndSampleWithMetadata(uint markerId, ulong sysTicks, byte metadataCount)
        {
            if (!PlayerConnectionProfiler.Enabled)
                return;

            Stream.buffer->MessageBegin(EditorMessageIds.kProfilerDataMessage);
            Stream.EmitSample(ProfilerMessageType.EndSampleWithMetadata, markerId, sysTicks, metadataCount);
            Stream.buffer->MessageEnd();
        }
    }

    // unity\Modules\Profiler\Runtime\Protocol.h/cpp
    // unity\Modules\Profiler\Runtime\PreThreadProfiler.h/cpp
    // [Header]
    // [ 
    //   [BlockHeader[Message ... Message]BlockFooter] ... [BlockHeader[Message ... Message]BlockFooter] 
    // ]
    public class ProfilerProtocolSession
    {
        // kProtocolVersion is the date of the last protocol modification
        // It should match the protocol we use as defined in big unity, and will enforce only working with versions
        // of unity that support it or later.
        static readonly internal uint kProtocolVersion = 0x20191122;

        static readonly internal uint kSessionGlobalHeader = 0x55334450;  // 'U3DP'
        static readonly internal IntPtr kSessionId = IntPtr.Zero - 1;

        static private uint profiledFrame = 0;

        static unsafe private ProfilerProtocolStream streamSession = new ProfilerProtocolStream();

        static unsafe internal void Initialize()
        {
            streamSession.Initialize(kSessionId);
        }

        static unsafe internal void Shutdown()
        {
            streamSession.Shutdown();
        }


        //---------------------------------------------------------------------------------------------------
        // Helpers
        //---------------------------------------------------------------------------------------------------
        static private int GetStringBytesCount(int byteCountUtf8)
        {
            // 32-bit string length + utf8 string data + 0-padded 32 bit byte alignment
            while ((byteCountUtf8 & 3) != 0)
                byteCountUtf8++;
            return 4 + byteCountUtf8;
        }

        static private unsafe void EmitStringUtf8(byte* textUtf8, int byteCountUtf8)
        {
            streamSession.buffer->WriteData<int>(byteCountUtf8);
            streamSession.buffer->WriteRaw(textUtf8, byteCountUtf8);
            while ((streamSession.buffer->DeferredSize & 3) != 0)
                streamSession.buffer->WriteData<byte>(0);
        }

        //---------------------------------------------------------------------------------------------------
        // Global Data (Profiling Session Information - must be sent with session "thread id")
        //---------------------------------------------------------------------------------------------------
        static private unsafe void EmitThreadInfo(ulong threadId, ulong sysTicksStart, bool frameIndependent, byte* groupUtf8, int groupBytes, byte* nameUtf8, int nameBytes)
        {
            // Thread info can be sent as session information or belonging to running thread

            int bytesData = 20 + GetStringBytesCount(groupBytes) + GetStringBytesCount(nameBytes);

            streamSession.EmitBlockBegin(ProfilerMessageType.ThreadInfo, bytesData);

            streamSession.buffer->WriteData<ulong>(threadId);
            streamSession.buffer->WriteData<ulong>(sysTicksStart);
            streamSession.buffer->WriteData<uint>(frameIndependent ? 1u : 0u);  // flags

            EmitStringUtf8(groupUtf8, groupBytes);
            EmitStringUtf8(nameUtf8, nameBytes);

            streamSession.EmitBlockEnd();
        }

        static private unsafe void EmitMarkerInfo(uint markerId, ushort categoryId, ushort flags, byte* nameUtf8, int nameBytes, byte metadataCount)
        {
            int bytesData = 8 + GetStringBytesCount(nameBytes) + 4;

            streamSession.EmitBlockBegin(ProfilerMessageType.MarkerInfo, bytesData);

            streamSession.buffer->WriteData<uint>(markerId);
            streamSession.buffer->WriteData<ushort>(flags);
            streamSession.buffer->WriteData<ushort>(categoryId);
            EmitStringUtf8(nameUtf8, nameBytes);
            streamSession.buffer->WriteData<uint>(metadataCount);

            streamSession.EmitBlockEnd();
        }

        static internal unsafe void EmitNewMarkersAndThreads()
        {
            // All markers we know about
            var markerBufferNode = Profiler.MarkersHeadBufferNode;
            while (markerBufferNode != null)
            {
                for (int i = 0; i < markerBufferNode->size; i++)
                {
                    var marker = &markerBufferNode->MarkersBuffer[i];
                    if (!marker->init && marker->nameBytes > 0)
                    {
                        EmitMarkerInfo(marker->markerId, marker->categoryId, marker->flags, marker->nameUtf8, marker->nameBytes, 0);
                        marker->init = true;
                    }
                }
                markerBufferNode = markerBufferNode->next;
            }

            // Main thread
            var threadBufferNode = Profiler.ThreadsHeadBufferNode;
            while (threadBufferNode != null)
            {
                for (int i = 0; i < threadBufferNode->size; i++)
                {
                    var thread = &threadBufferNode->ThreadsBuffer[i];
                    if (!thread->init && thread->nameBytes > 0)
                    {
                        EmitThreadInfo(thread->threadId, thread->sysTicksStart, thread->frameIndependent, 
                            thread->groupUtf8, thread->groupBytes, thread->nameUtf8, thread->nameBytes);
                        thread->init = true;
                    }
                }
                threadBufferNode = threadBufferNode->next;
            }
        }

        // Threadsafe but must be called from main thread
        static internal unsafe void SendProfilingSessionInfo()
        {
            ulong ticks = Profiler.GetProfilerTime();
            var conversion = Baselib_Timer_GetTicksToNanosecondsConversionRatio();

            streamSession.buffer->MessageBegin(EditorMessageIds.kProfilerDataMessage);

            // Global Session Header
            streamSession.buffer->WriteData<uint>(kSessionGlobalHeader);
            streamSession.buffer->WriteData<byte>(1);     // 1 = little endian  0 = big endian
            streamSession.buffer->WriteData<byte>(1);     // 1 = aligned memory access  0 = unaligned memory access
            streamSession.buffer->WriteData<ushort>(0);   // build target platform (not currently supported)
            streamSession.buffer->WriteData<uint>(kProtocolVersion);
            streamSession.buffer->WriteData<ulong>(conversion.ticksToNanosecondsNumerator);     // time numerator (multiply by this ratio to convert time to nanoseconds)
            streamSession.buffer->WriteData<ulong>(conversion.ticksToNanosecondsDenominator);   // time denominator (multiply by this ratio to convert time to nanoseconds)
            streamSession.buffer->WriteData<ulong>(ProfilerProtocolThread.Stream.threadId);   // main thread id

            // Profiling Session State
            // The Unity profiler has a handshake
            // 1) Editor->Player - Send a general PlayerConnectionMessage "ProfilerSetMode" 
            //                     (telling us profile is/isn't disabled according to user setup and which general things to profile)
            // 2) Player->Editor - Respond with a Profiler message describing current profiling state of the Player
            //                     (triggering the editor to enable the profiling session that the user has setup)
            streamSession.EmitBlockBegin(ProfilerMessageType.ProfilerState, 16);
            streamSession.buffer->WriteData<uint>(1);  // flags - currently only [0x01 : enabled] is defined
            streamSession.buffer->WriteData<ulong>(ticks);
            streamSession.buffer->WriteData<uint>(profiledFrame);
            streamSession.EmitBlockEnd();

            EmitNewMarkersAndThreads();

            streamSession.buffer->MessageEnd();
        }

        static public unsafe void SendNewMarkersAndThreads()
        {
            if (!PlayerConnectionProfiler.Enabled)
                return;

            if (!Profiler.NeedsUpdate)
                return;

            streamSession.buffer->MessageBegin(EditorMessageIds.kProfilerDataMessage);
            EmitNewMarkersAndThreads();
            streamSession.buffer->MessageEnd();

            Profiler.NeedsUpdate = false;
        }

        // Threadsafe but must be called from main thread
        static public unsafe void SendNewFrame()
        {
            // New frame must always belong to a "main thread" block and be the only message in the block
            if (!PlayerConnectionProfiler.Enabled)
                return;

            // This needs to happen with the main thread id and be the only message in the block - it is the only message
            // in the profiler system with either of these constraints
            ProfilerProtocolThread.Stream.buffer->MessageBegin(EditorMessageIds.kProfilerDataMessage);
            ProfilerProtocolThread.Stream.EmitBlockBegin(ProfilerMessageType.Frame, 12);
            ProfilerProtocolThread.Stream.buffer->WriteData<uint>(profiledFrame++);
            ProfilerProtocolThread.Stream.buffer->WriteData<ulong>(Profiler.GetProfilerTime());
            ProfilerProtocolThread.Stream.EmitBlockEnd();
            ProfilerProtocolThread.Stream.buffer->MessageEnd();
        }
    }

    public static class Profiler
    {
        private const int kHashChunkSize = 65536;

        // 1 marker info = 124/128 bytes (for 32/64 bit next pointers)
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct MarkerBucketNode
        {
            internal uint markerId;
            internal ushort flags;
            internal ushort categoryId;
            internal int nameBytes;
            internal fixed byte nameUtf8[103];
            internal bool init;
            internal MarkerBucketNode* next;
        }

        // Allocating chunks of 64k for Markers
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct MarkerHashTableBufferNode
        {
            internal fixed byte markersBuf[kHashChunkSize - 128];
            internal int capacity;
            internal int size;
            internal MarkerHashTableBufferNode* next;

            internal MarkerBucketNode* MarkersBuffer
            {
                get
                {
                    fixed (byte* b = markersBuf)
                    {
                        return (MarkerBucketNode*)b;
                    }
                }
            }
        }

        // 1 thread info = 124/128 bytes (for 32/64 bit next pointers)
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct ThreadBucketNode
        {
            internal ulong threadId;
            internal ulong sysTicksStart;
            internal int groupBytes;
            internal fixed byte groupUtf8[47];
            internal bool frameIndependent;
            internal int nameBytes;
            internal fixed byte nameUtf8[47];
            internal bool init;
            internal ThreadBucketNode* next;  // offset 120
        }

        // Allocating chunks of 64k for Threads
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct ThreadHashTableBufferNode
        {
            internal fixed byte threadsBuf[kHashChunkSize - 128];
            internal int capacity;
            internal int size;
            internal ThreadHashTableBufferNode* next;

            internal ThreadBucketNode* ThreadsBuffer
            {
                get
                {
                    fixed (byte* b = threadsBuf)
                    {
                        return (ThreadBucketNode*)b;
                    }
                }
            }
        }

        internal unsafe static MarkerHashTableBufferNode* MarkersHeadBufferNode => markerHashTableHead;
        internal unsafe static ThreadHashTableBufferNode* ThreadsHeadBufferNode => threadHashTableHead;

        private unsafe static MarkerHashTableBufferNode* markerHashTableHead = null;
        private unsafe static MarkerHashTableBufferNode* markerHashTableTail = null;
        private static uint nextMarkerId = 0;

        private unsafe static ThreadHashTableBufferNode* threadHashTableHead = null;
        private unsafe static ThreadHashTableBufferNode* threadHashTableTail = null;

        private static bool initialized = false;
        internal static bool NeedsUpdate { get; set; } = false;

        public static void Initialize()
        {
            if (initialized)
                return;
            PlayerConnectionProfiler.Initialize();
            ProfilerProtocolSession.Initialize();
            ThreadSetInfo((ulong)Baselib_Thread_GetCurrentThreadId(), GetProfilerTime(), false, "", "Main Thread");
            initialized = true;
        }

        public static void Shutdown()
        {
            if (!initialized)
                return;
            ProfilerProtocolThread.CleanAllThreads();
            ProfilerProtocolSession.Shutdown();
            PlayerConnectionProfiler.Shutdown();
            initialized = false;
        }

        public static ulong GetProfilerTime()
        {
            return (ulong)Baselib_Timer_GetHighPrecisionTimerTicks();
        }

        public static unsafe void MarkerBegin(IntPtr markerHandle, int metadata)
        {
            var data = new ProfilerMarkerData { Type = (byte)ProfilerMarkerDataType.Int32, Size = (uint)UnsafeUtility.SizeOf<int>(), Ptr = UnsafeUtility.AddressOf(ref metadata) };
            ProfilerUnsafeUtility.BeginSampleWithMetadata(markerHandle, 1, &data);
        }

        public static unsafe void MarkerBegin(IntPtr markerHandle, uint metadata)
        {
            var data = new ProfilerMarkerData { Type = (byte)ProfilerMarkerDataType.UInt32, Size = (uint)UnsafeUtility.SizeOf<uint>(), Ptr = UnsafeUtility.AddressOf(ref metadata) };
            ProfilerUnsafeUtility.BeginSampleWithMetadata(markerHandle, 1, &data);
        }

        public static unsafe void MarkerBegin(IntPtr markerHandle, long metadata)
        {
            var data = new ProfilerMarkerData { Type = (byte)ProfilerMarkerDataType.Int64, Size = (uint)UnsafeUtility.SizeOf<long>(), Ptr = UnsafeUtility.AddressOf(ref metadata) };
            ProfilerUnsafeUtility.BeginSampleWithMetadata(markerHandle, 1, &data);
        }

        public static unsafe void MarkerBegin(IntPtr markerHandle, ulong metadata)
        {
            var data = new ProfilerMarkerData { Type = (byte)ProfilerMarkerDataType.UInt64, Size = (uint)UnsafeUtility.SizeOf<ulong>(), Ptr = UnsafeUtility.AddressOf(ref metadata) };
            ProfilerUnsafeUtility.BeginSampleWithMetadata(markerHandle, 1, &data);
        }

        public static unsafe void MarkerBegin(IntPtr markerHandle, float metadata)
        {
            var data = new ProfilerMarkerData { Type = (byte)ProfilerMarkerDataType.Float, Size = (uint)UnsafeUtility.SizeOf<float>(), Ptr = UnsafeUtility.AddressOf(ref metadata) };
            ProfilerUnsafeUtility.BeginSampleWithMetadata(markerHandle, 1, &data);
        }

        public static unsafe void MarkerBegin(IntPtr markerHandle, double metadata)
        {
            var data = new ProfilerMarkerData { Type = (byte)ProfilerMarkerDataType.Double, Size = (uint)UnsafeUtility.SizeOf<double>(), Ptr = UnsafeUtility.AddressOf(ref metadata) };
            ProfilerUnsafeUtility.BeginSampleWithMetadata(markerHandle, 1, &data);
        }

        public static unsafe void MarkerBegin(IntPtr markerHandle, string metadata)
        {
            var data = new ProfilerMarkerData { Type = (byte)ProfilerMarkerDataType.String16 };
            fixed (char* c = metadata)
            {
                data.Size = ((uint)metadata.Length + 1) * 2;
                data.Ptr = c;
                ProfilerUnsafeUtility.BeginSampleWithMetadata(markerHandle, 1, &data);
            }
        }

        // Burst/Thread safe
        internal static unsafe string MarkerGetStringName(IntPtr markerPtr)
        {
            MarkerBucketNode* marker = (MarkerBucketNode*)markerPtr;
            int charCount = UTF8.GetCharCount(marker->nameUtf8, marker->nameBytes);
            char* chars = stackalloc char[charCount];
            UTF8.GetChars(marker->nameUtf8, marker->nameBytes, chars, charCount);
            return new string(chars);
        }

        // @@TODO need threadsafe/burstsafe
        internal static unsafe void* MarkerGetOrCreate(ushort categoryId, byte* name, int nameBytes, ushort flags)
        {
            if (nameBytes <= 0)
                return null;

            if (nextMarkerId == 0)
            {
                markerHashTableHead = (MarkerHashTableBufferNode*)UnsafeUtility.Malloc(kHashChunkSize, 16, Allocator.Persistent);
                *markerHashTableHead = new MarkerHashTableBufferNode();
                markerHashTableHead->capacity = (kHashChunkSize - 128) / sizeof(MarkerBucketNode);
                markerHashTableHead->size = 256;  // number of buckets
                markerHashTableTail = markerHashTableHead;
            }

            int bucket = (((nameBytes << 5) + (nameBytes >> 2)) ^ name[0]) & 255;
            MarkerBucketNode* next = &markerHashTableHead->MarkersBuffer[bucket];
            MarkerBucketNode* marker = null;
            while (next != null)
            {
                marker = next;
                next = marker->next;

                if (marker->nameBytes == nameBytes)
                {
                    if (UnsafeUtility.MemCmp(name, marker->nameUtf8, nameBytes) == 0)
                    {
                        // Make sure category is up to date
                        marker->categoryId = categoryId;
                        marker->flags = flags;
                        return marker;
                    }
                }
            }

            if (marker->nameBytes > 0)
            {
                // There is already a valid marker here at the end of the linked list - add a new one
                if (markerHashTableHead->size == markerHashTableHead->capacity)
                {
                    MarkerHashTableBufferNode* newPool = (MarkerHashTableBufferNode*)UnsafeUtility.Malloc(kHashChunkSize, 16, Allocator.Persistent);
                    UnsafeUtility.MemClear(newPool, kHashChunkSize);
                    newPool->capacity = (kHashChunkSize - 128) / sizeof(MarkerBucketNode);
                    markerHashTableTail->next = newPool;
                    markerHashTableTail = newPool;
                }

                MarkerBucketNode* newMarker = &markerHashTableTail->MarkersBuffer[markerHashTableTail->size];
                markerHashTableTail->size++;
                marker->next = newMarker;
                marker = newMarker;
            }

            marker->init = false;
            marker->categoryId = categoryId;
            marker->flags = flags;
            marker->markerId = nextMarkerId++;
            marker->nameBytes = nameBytes;
            UnsafeUtility.MemCpy(marker->nameUtf8, name, nameBytes);

            NeedsUpdate = true;

            return marker;
        }

        // Burst safe
        // @@TODO need threadsafe
        internal static unsafe void MarkerBegin(void* markerPtr, void* metadata, int metadataBytes)
        {
            MarkerBucketNode* marker = (MarkerBucketNode*)markerPtr;
            ProfilerProtocolThread.SendBeginSample(marker->markerId, Profiler.GetProfilerTime());
        }

        // Burst safe
        // @@TODO need threadsafe
        internal static unsafe void MarkerEnd(void* markerPtr)
        {
            MarkerBucketNode* marker = (MarkerBucketNode*)markerPtr;
            ProfilerProtocolThread.SendEndSample(marker->markerId, Profiler.GetProfilerTime());
        }

        // NOT Burst/Thread safe
        internal static unsafe void ThreadSetInfo(ulong threadId, ulong sysTicksStart, bool frameIndependent, string threadGroup, string threadName)
        {
            if (threadHashTableHead == null)
            {
                threadHashTableHead = (ThreadHashTableBufferNode*)UnsafeUtility.Malloc(kHashChunkSize, 16, Allocator.Persistent);
                *threadHashTableHead = new ThreadHashTableBufferNode();
                threadHashTableHead->capacity = (kHashChunkSize - 128) / sizeof(ThreadBucketNode);
                threadHashTableHead->size = 256;  // number of buckets
                threadHashTableTail = threadHashTableHead;
            }

            int bucket = (int)threadId & 255;
            ThreadBucketNode* next = &threadHashTableHead->ThreadsBuffer[bucket];
            ThreadBucketNode* thread = null;
            while (next != null)
            {
                thread = next;
                next = thread->next;

                if (thread->threadId == threadId)
                {
                    // Make sure info is up to date
                    fixed (char* c = threadGroup)
                    {
                        thread->groupBytes = UTF8.GetBytes(c, threadGroup.Length, thread->groupUtf8, 47);
                    }
                    fixed (char* c = threadName)
                    {
                        thread->nameBytes = UTF8.GetBytes(c, threadName.Length, thread->nameUtf8, 47);
                    }
                    thread->frameIndependent = frameIndependent;
                    return;
                }
            }

            if (thread->nameBytes > 0 || thread->groupBytes > 0)
            {
                // There is already a valid thread here at the end of the linked list - add a new one
                if (threadHashTableHead->size == threadHashTableHead->capacity)
                {
                    ThreadHashTableBufferNode* newPool = (ThreadHashTableBufferNode*)UnsafeUtility.Malloc(kHashChunkSize, 16, Allocator.Persistent);
                    UnsafeUtility.MemClear(newPool, kHashChunkSize);
                    newPool->capacity = (kHashChunkSize - 128) / sizeof(ThreadBucketNode);
                    threadHashTableTail->next = newPool;
                    threadHashTableTail = newPool;
                }

                ThreadBucketNode* newThread = &threadHashTableTail->ThreadsBuffer[threadHashTableTail->size];
                threadHashTableTail->size++;
                thread->next = newThread;
                thread = newThread;
            }

            thread->init = false;
            fixed (char* c = threadGroup)
            {
                thread->groupBytes = UTF8.GetBytes(c, threadGroup.Length, thread->groupUtf8, 47);
            }
            fixed (char* c = threadName)
            {
                thread->nameBytes = UTF8.GetBytes(c, threadName.Length, thread->nameUtf8, 47);
            }
            thread->frameIndependent = frameIndependent;
            thread->sysTicksStart = sysTicksStart;
            thread->threadId = threadId;

            NeedsUpdate = true;
        }
    }
}

#endif
