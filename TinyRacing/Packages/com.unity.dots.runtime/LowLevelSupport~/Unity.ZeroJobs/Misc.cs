using System;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe;
#if ENABLE_PLAYERCONNECTION
using Unity.Development.PlayerConnection;
using Unity.Development;
#endif

//unity.properties has an unused "using UnityEngine.Bindings".
namespace UnityEngine.Bindings
{
    public class Dummy
    {
    }
}

namespace UnityEngine.Internal
{
    public class ExcludeFromDocsAttribute : Attribute {}
}

namespace Unity.Baselib.LowLevel
{
    public static class BaselibNativeLibrary
    {
        public const string DllName = JobsUtility.nativejobslib;
    }
}

namespace System
{
    public class CodegenShouldReplaceException : NotImplementedException
    {
        public CodegenShouldReplaceException() : base("This function should have been replaced by codegen")
        {
        }

        public CodegenShouldReplaceException(string msg) : base(msg)
        {
        }
    }
}

namespace Unity.Core
{
    public static class DotsRuntime
    {
#if ENABLE_PROFILER
        private static Unity.Profiling.ProfilerMarker rootMarker = new Profiling.ProfilerMarker("Hidden main root");
        private static Unity.Profiling.ProfilerMarker mainMarker = new Profiling.ProfilerMarker("Main Thread Frame");
#endif
        private static bool firstFrame = true;

        public static void Initialize()
        {
            JobsUtility.Initialize();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Initialize();
#endif
#if ENABLE_PLAYERCONNECTION
            Connection.Initialize();
            Logger.Initialize();
#endif
#if ENABLE_PROFILER
            Profiler.Initialize();
#endif

            firstFrame = true;
        }

        public static void Shutdown()
        {
            JobsUtility.Shutdown();

#if ENABLE_PROFILER
            Profiler.Shutdown();
#endif
#if ENABLE_PLAYERCONNECTION
            Connection.Shutdown();
#endif
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Shutdown();
#endif
        }

        public static void UpdatePreFrame()
        {
            if (firstFrame)
            {
#if ENABLE_PROFILER
                ProfilerProtocolSession.SendNewFrame();
                rootMarker.Begin();
                mainMarker.Begin();
#endif
                firstFrame = false;
            }
        }

        public static void UpdatePostFrame(bool willContinue)
        {
            UnsafeUtility.FreeTempMemory();

#if ENABLE_PROFILER
            mainMarker.End();
            rootMarker.End();

            ProfilerProtocolSession.SendNewMarkersAndThreads();
#endif

#if ENABLE_PLAYERCONNECTION
            Connection.TransmitAndReceive();
#endif

#if ENABLE_PROFILER
            if (willContinue)
            {
                ProfilerProtocolSession.SendNewFrame();
                rootMarker.Begin();
                mainMarker.Begin();
            }
#endif
        }
    }
}
