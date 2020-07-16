using System;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;

namespace UnityEngine.Profiling
{
    public class CustomSampler
    {
        public static CustomSampler Create(string s) => throw new NotImplementedException();
        public void Begin() => throw new NotImplementedException();
        public void End() => throw new NotImplementedException();
    }

    public static class Profiler
    {
        // @@TODO This either needs to be burstable, or we need to kill UnityEngine.Profiling and exclusively use markers.
        //        We can resolve burst-ability as well as thread contention once we have a general AtomicStack interface exposed,
        //        which we can do once we have SharedStatic in ZeroJobs
        public static IntPtr[] beginStack = new IntPtr[32];
        public static int stackPos = 0;

        public static void BeginSample(string s)
        {
#if ENABLE_PROFILER
            if (stackPos == beginStack.Length)
                throw new InvalidOperationException("Too many nested UnityEngine.Profiling.Profiler.BeginSample() calls");

            // Just gets the marker if it already exists
            IntPtr marker = ProfilerUnsafeUtility.CreateMarker(s, ProfilerUnsafeUtility.InternalCategoryInternal, MarkerFlags.Default, 0);
            ProfilerUnsafeUtility.BeginSample(marker);
            beginStack[stackPos++] = marker;
#endif
        }

        public static void EndSample()
        {
#if ENABLE_PROFILER
            if (stackPos == 0)
                throw new InvalidOperationException("Too many UnityEngine.Profiling.Profiler.EndSample() calls (no matching BeginSample)");

            stackPos--;
            ProfilerUnsafeUtility.EndSample(beginStack[stackPos]);
#endif
        }
    }
}
