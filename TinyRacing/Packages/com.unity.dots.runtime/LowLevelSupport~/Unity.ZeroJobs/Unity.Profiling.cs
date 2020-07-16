using System;
using System.Runtime.InteropServices;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe;
using static System.Text.Encoding;
#if ENABLE_PROFILER
using Unity.Development;
#endif

namespace Unity.Profiling
{
    public class Profiler
    {
        // @@todo remove but they are in dots-platforms
        static public void FrameBegin()
        {

        }
        static public void FrameEnd()
        {

        }
    }

    //-------------------------------------------------------------------------------------------------------
    // unity\Runtime\Profiler\ScriptBindings\ProfilerMarker.bindings.cs
    //-------------------------------------------------------------------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    public struct ProfilerMarker
    {
        internal readonly IntPtr m_Ptr;
        public IntPtr Handle => m_Ptr;

        public ProfilerMarker(string name)
        {
            m_Ptr = ProfilerUnsafeUtility.CreateMarker(name, ProfilerUnsafeUtility.CategoryScripts, MarkerFlags.Default, 0);
        }

        public void Begin()
        {
#if ENABLE_PROFILER
            // Early out as soon as possible if profiler disabled
            if (!PlayerConnectionProfiler.Enabled)
                return;
            ProfilerUnsafeUtility.BeginSample(m_Ptr);
#endif
        }

        public void End()
        {
#if ENABLE_PROFILER
            // Early out as soon as possible if profiler disabled
            if (!PlayerConnectionProfiler.Enabled)
                return;
            ProfilerUnsafeUtility.EndSample(m_Ptr);
#endif
        }

        internal void GetName(ref string name)
        {
#if ENABLE_PROFILER
            name = Development.Profiler.MarkerGetStringName(m_Ptr);
#endif
        }

        public struct AutoScope : IDisposable
        {
            [NativeDisableUnsafePtrRestriction]
            internal readonly IntPtr m_Ptr;

            internal AutoScope(IntPtr markerPtr)
            {
                m_Ptr = markerPtr;
#if ENABLE_PROFILER
                ProfilerUnsafeUtility.BeginSample(markerPtr);
#endif
            }

            public void Dispose()
            {
#if ENABLE_PROFILER
                ProfilerUnsafeUtility.EndSample(m_Ptr);
#endif
            }
        }

        public AutoScope Auto()
        {
            return new AutoScope(m_Ptr);
        }
    }
}


//-------------------------------------------------------------------------------------------------------
// unity\Runtime\Profiler\ScriptBindings\ProfilerUtility.cs
//-------------------------------------------------------------------------------------------------------
namespace Unity.Profiling.LowLevel
{
    // Profiler marker usage flags.
    // Must be in sync with UnityProfilerMarkerFlags!
    [Flags]
    public enum MarkerFlags : ushort
    {
        Default = 0,                         // Static and enabled by default

        Script = 1 << 1,                     // In user scripts
        ScriptInvoke = 1 << 5,               // Runtime invocations with ScriptingInvocation::Invoke @@TODO does this apply to DOTSRT?
        ScriptDeepProfiler = 1 << 6,         // Deep profiler @@TODO not sure how we'd do that yet

        AvailabilityEditor = 1 << 2,         // Only when played in editor (no builds) @@TODO should we support?
        AvailabliltyNonDevelopment = 1 << 3, // Works in release builds @@TODO should we support?
        
        Warning = 1 << 4,                    // If hit, indicates undesirable, performance-wise unoptimal code path @@TODO
        Counter = 1 << 7,                    // Marker is a counter @@TODO

        // Bits 10-12 for verbosity levels. Allows to filter markers during visualization.
        // By default marker has user visibility - Scripting, Camera.Render, etc.
        VerbosityDebug = 1 << 10,            // Internal debug markers - jobsystem idle @@ TODO support?
        VerbosityInternal = 1 << 11,         // Internal markers - mutex/semaphore waits @@ TODO support?
        VerbosityAdvanced = 1 << 12,         // Markers useful for advanced users - loading @@ TODO support?
    }

    // Supported profiler metadata types.
    // Must be in sync with UnityProfilerMarkerDataType!
    public enum ProfilerMarkerDataType : byte
    {
        Int32 = 2,
        UInt32 = 3,
        Int64 = 4,
        UInt64 = 5,
        Float = 6,
        Double = 7,
        String16 = 9,
        Blob8 = 11,
    }
}


//-------------------------------------------------------------------------------------------------------
// unity\Runtime\Profiler\ScriptBindings\ProfilerUnsafeUtility.cs
//-------------------------------------------------------------------------------------------------------
namespace Unity.Profiling.LowLevel.Unsafe
{
    // Metadata parameter.
    // Must be in sync with UnityProfilerMarkerData!
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public unsafe struct ProfilerMarkerData
    {
        [FieldOffset(0)] public byte Type;
        [FieldOffset(1)] readonly byte reserved0;
        [FieldOffset(2)] readonly ushort reserved1;
        [FieldOffset(4)] public uint Size;
        [FieldOffset(8)] public void* Ptr;
    };

    public static class ProfilerUnsafeUtility
    {
        // Built-in profiler categories.
        // Must be in sync with profiling::BuiltinCategory!
        public const ushort CategoryRender = 0;
        public const ushort CategoryScripts = 1;
        public const ushort CategoryGUI = 4;
        public const ushort CategoryPhysics = 5;
        public const ushort CategoryAnimation = 6;
        public const ushort CategoryAi = 7;
        public const ushort CategoryAudio = 8;
        public const ushort CategoryVideo = 11;
        public const ushort CategoryParticles = 12;
        public const ushort CategoryGI = 13;
        public const ushort CategoryNetwork = 14;
        public const ushort CategoryLoading = 15;
        public const ushort CategoryOther = 16;
        public const ushort CategoryVR = 22;
        public const ushort CategoryAllocation = 23;
        public const ushort CategoryInput = 30;

        // These are not exposed in ProfilerUnsafeUtility in Unity, but we will likely use them
        public const ushort InternalCategoryManagedJobs = 2;
        public const ushort InternalCategoryBurstJobs = 3;
        public const ushort InternalCategoryAudioJob = 9;
        public const ushort InternalCategoryAudioUpdateJob = 10;
        public const ushort InternalCategoryGc = 17;
        public const ushort InternalCategoryVsync = 18;
        public const ushort InternalCategoryOverhead = 19;
        public const ushort InternalCategoryPlayerloop = 20;
        public const ushort InternalCategoryDirector = 21;
        public const ushort InternalCategoryInternal = 24;
        public const ushort InternalCategoryFileIo = 25;
        public const ushort InternalCategoryUiSystemLayout = 26;
        public const ushort InternalCategoryUiSystemRender = 27;
        public const ushort InternalCategoryVfx = 28;
        public const ushort InternalCategoryBuildInterface = 29;
        public const ushort InternalCategoryVirtualTexturing = 31;

        public static IntPtr CreateMarker(string name, ushort categoryId, MarkerFlags flags, int metadataCount)
        {
#if ENABLE_PROFILER
            int textBytes = UTF8.GetByteCount(name);
            unsafe
            {
                byte* bytes = stackalloc byte[textBytes];
                fixed (char* t = name)
                {
                    UTF8.GetBytes(t, name.Length, bytes, textBytes);
                }
                return (IntPtr)Development.Profiler.MarkerGetOrCreate(categoryId, bytes, textBytes, (ushort)(flags | MarkerFlags.Script));
            }
#else
            return IntPtr.Zero;
#endif
        }

        // @@TODO impl
        //public static void SetMarkerMetadata(IntPtr markerPtr, int index, string name, byte type, byte unit);

        public static void BeginSample(IntPtr markerPtr)
        {
#if ENABLE_PROFILER
            unsafe
            {
                Development.Profiler.MarkerBegin((void*)markerPtr, null, 0);
            }
#endif
        }

        public static unsafe void BeginSampleWithMetadata(IntPtr markerPtr, int metadataCount, void* metadata)
        {
#if ENABLE_PROFILER
            unsafe
            {
                Development.Profiler.MarkerBegin((void*)markerPtr, metadata, metadataCount);
            }
#endif
        }

        public static void EndSample(IntPtr markerPtr)
        {
#if ENABLE_PROFILER
            unsafe
            {
                Development.Profiler.MarkerEnd((void*)markerPtr);
            }
#endif
        }

        // @@TODO impl
        //public static unsafe void SingleSampleWithMetadata(IntPtr markerPtr, int metadataCount, void* metadata);

        // @@TODO impl
        //public static unsafe void* CreateCounterValue(out IntPtr counterPtr, string name, ushort categoryId, MarkerFlags flags, byte dataType, byte dataUnit, int dataSize, ProfilerCounterOptions counterOptions);

        // @@TODO impl
        //public static unsafe void FlushCounterValue(void* counterValuePtr);
    }
}
