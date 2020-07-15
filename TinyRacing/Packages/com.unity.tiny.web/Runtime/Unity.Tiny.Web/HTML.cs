using System;
using System.Diagnostics;
using Unity.Entities;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Tiny.Web
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public class HTMLWindowSystem : WindowSystem
    {
        public HTMLWindowSystem()
        {
            initialized = false;
        }

        public override void DebugReadbackImage(out int w, out int h, out NativeArray<byte> pixels)
        {
            var env = World.TinyEnvironment();
            var config = env.GetConfigData<DisplayInfo>();
            pixels = new NativeArray<byte>(config.framebufferWidth * config.framebufferHeight * 4, Allocator.Persistent);
            unsafe
            {
                HTMLNativeCalls.debugReadback(config.framebufferWidth, config.framebufferHeight, pixels.GetUnsafePtr());
            }

            w = config.framebufferWidth;
            h = config.framebufferHeight;
        }

        IntPtr m_PlatformCanvasName;
        public override IntPtr GetPlatformWindowHandle()
        {
            if (m_PlatformCanvasName == IntPtr.Zero)
            {
                m_PlatformCanvasName = Marshal.StringToCoTaskMemAnsi("#UT_CANVAS");
            }
            return m_PlatformCanvasName;
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            if (initialized)
                return;
#if DEBUG
            Debug.Log("HTML Window init.");
#endif
            try
            {
                initialized = HTMLNativeCalls.init();
            }
            catch
            {
                Console.WriteLine("  Excepted (Is lib_unity_tiny2d_html.dll missing?).");
                initialized = false;
            }
            if (!initialized)
            {
                Console.WriteLine("  Failed.");
                return;
            }

            UpdateDisplayInfo(firstTime: true);
        }

        protected override void OnDestroy()
        {
            // close window
            Console.WriteLine("HTML Window shutdown.");
            HTMLNativeCalls.shutdown(0);
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            if (!initialized)
                return;

            UpdateDisplayInfo(firstTime: false);

            var env = World.TinyEnvironment();
            double newFrameTime = HTMLNativeCalls.time();
            var timeData = env.StepWallRealtimeFrame(newFrameTime - frameTime);
            World.SetTime(timeData);
            frameTime = newFrameTime;
        }

        private void UpdateDisplayInfo(bool firstTime)
        {
            var env = World.TinyEnvironment();
            var di = env.GetConfigData<DisplayInfo>();

            // TODO DOTSR-994 -- screenDpiScale is being used as both user configuration and information here
            if (di.screenDpiScale == 0.0f)
                di.screenDpiScale = HTMLNativeCalls.getDPIScale();

            HTMLNativeCalls.getScreenSize(ref di.screenWidth, ref di.screenHeight);
            HTMLNativeCalls.getFrameSize(ref di.frameWidth, ref di.frameHeight);

            int wCanvas = 0, hCanvas = 0;
            if (firstTime)
            {
                // TODO DOTSR-994 -- this is a case where we're using width/height as read/write instead of as explicit read or write only
                wCanvas = di.width;
                hCanvas = di.height;
            }
            else
            {
                HTMLNativeCalls.getCanvasSize(ref wCanvas, ref hCanvas);
            }

            if (di.autoSizeToFrame)
            {
                di.width = di.frameWidth;
                di.height = di.frameHeight;
            }

            // TODO DOTSR-994 -- the framebufferWidth/Height should be directly configurable
            di.framebufferWidth = (int)(di.width * di.screenDpiScale);
            di.framebufferHeight = (int)(di.height * di.screenDpiScale);

            unsafe
            {
                if (firstTime || UnsafeUtility.MemCmp(UnsafeUtility.AddressOf(ref di), UnsafeUtility.AddressOf(ref lastDisplayInfo), sizeof(DisplayInfo)) != 0)
                {
                    // Only do this if it's the first time, or if the struct values actually changed from the last time we set it
#if DEBUG
                    Debug.Log($"setCanvasSize {di.width}px {di.height}px (backing {di.framebufferWidth} {di.framebufferHeight}, dpi scale {di.screenDpiScale})");
#endif
                    HTMLNativeCalls.setCanvasSize(di.width, di.height, di.framebufferWidth, di.framebufferHeight);
                    env.SetConfigData(di);
                    lastDisplayInfo = di;
                }
            }
        }

        protected DisplayInfo lastDisplayInfo;
        protected bool initialized;
        protected double frameTime;
    }

    static class HTMLNativeCalls
    {
        // calls to HTMLWrapper.cpp
        [DllImport("lib_unity_tiny_web", EntryPoint = "init_html")]
        [return : MarshalAs(UnmanagedType.I1)]
        public static extern bool init();

        [DllImport("lib_unity_tiny_web", EntryPoint = "shutdown_html")]
        public static extern void shutdown(int exitCode);

        [DllImport("lib_unity_tiny_web", EntryPoint = "time_html")]
        public static extern double time();

        // calls to HTMLWrapper.js directly
        [DllImport("lib_unity_tiny_web", EntryPoint = "js_html_setCanvasSize")]
        public static extern int setCanvasSize(int cssWidth, int cssHeight, int fbWidth, int fbHeight);

        [DllImport("lib_unity_tiny_web", EntryPoint = "js_html_debugReadback")]
        public static unsafe extern void debugReadback(int w, int h, void *pixels);

        [DllImport("lib_unity_tiny_web", EntryPoint = "js_html_getCanvasSize")]
        public static extern void getCanvasSize(ref int w, ref int h);

        [DllImport("lib_unity_tiny_web", EntryPoint = "js_html_getFrameSize")]
        public static extern void getFrameSize(ref int w, ref int h);

        [DllImport("lib_unity_tiny_web", EntryPoint = "js_html_getScreenSize")]
        public static extern void getScreenSize(ref int w, ref int h);

        [DllImport("lib_unity_tiny_web", EntryPoint = "js_html_getDPIScale")]
        public static extern float getDPIScale();
    }
}
