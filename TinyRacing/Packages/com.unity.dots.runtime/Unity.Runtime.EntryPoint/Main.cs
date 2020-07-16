using System;
#if UNITY_DOTSPLAYER_IL2CPP_WAIT_FOR_MANAGED_DEBUGGER && !UNITY_WEBGL
using Unity.Development.PlayerConnection;
#endif
using Unity.Platforms;

namespace Unity.Tiny.EntryPoint
{
    public static class Program
    {
        private static void Main()
        {
            var unity = UnityInstance.Initialize();

            unity.OnTick = () =>
            {
                var shouldContinue = unity.Update();
                if (shouldContinue == false)
                {
                    unity.Deinitialize();
                }
                return shouldContinue;
            };

#if UNITY_DOTSPLAYER_IL2CPP_WAIT_FOR_MANAGED_DEBUGGER && !UNITY_WEBGL
            DebuggerAttachDialog.Show(Connection.TransmitAndReceive);
#endif

            RunLoop.EnterMainLoop(unity.OnTick);
        }
    }
}
