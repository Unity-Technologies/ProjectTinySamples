#if ENABLE_PLAYERCONNECTION

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using static Unity.Baselib.LowLevel.Binding;
using UnityEngine.Events;
using static System.Text.Encoding;

namespace Unity.Development.PlayerConnection
{
    // Unity Guid is in byte order of string version with nibbles swapped
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct UnityGuid
    {
        private fixed byte data[16];

        public UnityGuid(byte b0, byte b1, byte b2, byte b3, byte b4, byte b5, byte b6, byte b7, byte b8, byte b9, byte b10, byte b11, byte b12, byte b13, byte b14, byte b15)
        {
            data[0] = b0;
            data[1] = b1;
            data[2] = b2;
            data[3] = b3;
            data[4] = b4;
            data[5] = b5;
            data[6] = b6;
            data[7] = b7;
            data[8] = b8;
            data[9] = b9;
            data[10] = b10;
            data[11] = b11;
            data[12] = b12;
            data[13] = b13;
            data[14] = b14;
            data[15] = b15;
        }

        public UnityGuid(Guid guid)
        {
            fixed (byte* s = guid.ToByteArray())
            {
                fixed (byte* d = data)
                {
                    Convert(d, s);
                }
            }
        }
        public static implicit operator UnityGuid(Guid guid) { return new UnityGuid(guid); }
        public Guid ToGuid()
        {
            byte[] dest = new byte[16];
            fixed (byte* s = data)
            {
                fixed (byte* d = dest)
                {
                    Convert(d, s);
                }
            }
            return new Guid(dest);
        }

        unsafe public UnityGuid(string guidString)
        {
            byte[] k_LiteralToHex =
            {
                0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
                0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
                0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09,
                0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
                0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f,
                0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
                0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f,
                0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
                0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
                0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
                0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
                0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
                0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff
            };

            // Convert every hex char into an int [0...16]
            var hex = stackalloc byte[32];
            for (int i = 0; i < 32; i++)
            {
                int intValue = guidString[i];
                if (intValue < 0 || intValue > 255)
                    return;

                hex[i] = k_LiteralToHex[intValue];
            }

            for (int i = 0; i < 16; i++)
            {
                data[i] = (byte)(hex[i * 2] | (hex[i * 2 + 1] << 4));
            }
        }

        public static bool operator ==(UnityGuid a, UnityGuid b)
        {
            for (int i = 0; i < 16; i++)
            {
                if (a.data[i] != b.data[i])
                    return false;
            }
            return true;
        }

        public static bool operator !=(UnityGuid a, UnityGuid b)
        {
            return !(a == b);
        }

        private void Convert(byte* dest, byte* src)
        {
            int[] k_Swap = { 3, 2, 1, 0, 5, 4, 7, 6, 8, 9, 10, 11, 12, 13, 14, 15 };
            for (int i = 0; i < 16; i++)
            {
                byte b = src[k_Swap[i]];
                dest[i] = (byte)((b >> 4) | (b << 4));
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is UnityGuid guid)
                return this == guid;
            throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }

    internal static class EditorPorts
    {
        // Game initiates connection to Unity Editor
        // - Good for localhost
        // - Good for external Unity Editor host's IP if known
        public const ushort DirectConnect = 34999;

        // Manually initiated from Unity Editor
        //   or Automatically initiated from Unity Editor after a current-session multicast is received
        // (Unity attempts a range of 512 ports when user manually initiates connection)
        // - Good for custom IPs such as mobile or web
        public const ushort DirectListenFirst = 55000;
        public const ushort DirectListenLast = 55511;

        // Automatically initiated from Unity Editor
        // (We must broadcast first with a specifically formatted message to notify we are a valid target for connection)
        // - Best choice for local networks or localhost
        public const ushort Multicast = 54997;

        // Unity Websockify proxy-server listens on this port to initiate a connection
        // to the TCP socket port for 'DirectConnect' above
        public const ushort WebSocketProxy = 54998;
    }

    // Unused messages are commented out while we sort out what is relevant
    // Using raw bytes instead of strings for burst support
    internal static class EditorMessageIds
    {
        public const uint kMagicNumber = 0x67A54E8F;

        public static readonly UnityGuid kLog = new UnityGuid(0x93, 0xa4, 0xad, 0x30, 0xb8, 0x0a, 0xf4, 0x62, 0x0b, 0x10, 0xa1, 0xc6, 0xed, 0x0b, 0xa5, 0x26); //"394ada038ba04f26b0011a6cdeb05a62");
        //public static readonly UnityGuid kCleanLog = new UnityGuid("3ded2ddacdf246d8a3f601741741e7a9");

        //public static readonly UnityGuid kFileTransfer = new UnityGuid("c2a22f5d7091478ab4d6c163a7573c35");
        //public static readonly UnityGuid kFrameDebuggerEditorToPlayer = new UnityGuid("035c0cae2e03494894aabe3955d4bf43");
        //public static readonly UnityGuid kFrameDebuggerPlayerToEditor = new UnityGuid("8f448ceb744d42ba80a854f56e43b77e");

        public static readonly UnityGuid kPingAlive = new UnityGuid(0xef, 0xb9, 0x81, 0x21, 0xf7, 0x06, 0x54, 0x6c, 0xd8, 0x2b, 0x03, 0x9d, 0x39, 0x2d, 0x2a, 0x01); //"fe9b18127f6045c68db230d993d2a210");  // Won't get a response, but will fail if no connection made (since connection is async)
        public static readonly UnityGuid kApplicationQuit = new UnityGuid(0x83, 0x5a, 0x2d, 0x64, 0x05, 0x56, 0x64, 0xfd, 0xea, 0xbd, 0x66, 0x35, 0x6f, 0x2e, 0xb2, 0x33); //"38a5d246506546dfaedb6653f6e22b33");   // notify editor we quit, or editor notifying when it quits
        public static readonly UnityGuid kNoFurtherConnections = new UnityGuid(0x26, 0xab, 0x06, 0x37, 0x9d, 0x70, 0x24, 0x96, 0x59, 0x7e, 0x86, 0x2e, 0x8c, 0x2a, 0x3b, 0x86); //"62ba6073d907426995e768e2c8a2b368");

        public static readonly UnityGuid kProfilerSetMode = new UnityGuid(0x22, 0x75, 0x64, 0xd6, 0xe0, 0xe0, 0x74, 0xad, 0x98, 0x28, 0xc6, 0x0f, 0xe4, 0x86, 0x31, 0xc5); //"2257466d0e0e47da89826cf04e68135c");  // editor usually sends this as soon as we connect - 1
        public static readonly UnityGuid kProfilerDataMessage = new UnityGuid(0x5c, 0xd8, 0x77, 0x81, 0xf4, 0xb4, 0xb4, 0x95, 0x3b, 0xff, 0xcf, 0xf6, 0x08, 0xa0, 0x1e, 0xe0); //"c58d77184f4b4b59b3fffc6f800ae10e");  // We send this with profiler data
        public static readonly UnityGuid kProfilerSetMemoryRecordMode = new UnityGuid(0x4c, 0xd8, 0x90, 0xf7, 0xf8, 0xae, 0x64, 0x43, 0x49, 0x8b, 0x0f, 0xb8, 0xb0, 0x55, 0x0d, 0xa5); //"c48d097f8fea463494b8f08b0b55d05a");  // editor usually sends this as soon as we connect - 3
        //public static readonly UnityGuid kProfilerSetAutoInstrumentedAssemblies = new UnityGuid("6cfdfe5ac10d4b79bfe27e8abe06915f");  // editor usually sends this as soon as we connect - 2
        //public static readonly UnityGuid kProfilerSetAudioCaptureFlags = new UnityGuid("1e792ecb5c9f4a8381d0d03528b6ae7b");
        //public static readonly UnityGuid kProfilerQueryInstrumentableFunctions = new UnityGuid("302b3998e168478eb8713b086c7693a9");
        //public static readonly UnityGuid kProfilerQueryFunctionCallees = new UnityGuid("d8f38a5539cc4b608792c273efe6a969");
        //public static readonly UnityGuid kProfilerFunctionsDataMessage = new UnityGuid("e2acb618e8c8465a901eb7b6f667cc41");
        //public static readonly UnityGuid kProfilerBeginInstrumentFunction = new UnityGuid("027723bb8a12495aa4803c27d10c86b8");
        //public static readonly UnityGuid kProfilerEndInstrumentFunction = new UnityGuid("1db84608522147b8bc57e34cd4d036b1");
        //public static readonly UnityGuid kObjectMemoryProfileSnapshot = new UnityGuid("14473694eb0a4963870aaab63efb7507");
        //public static readonly UnityGuid kObjectMemoryProfileDataMessage = new UnityGuid("8584ee18ea264718873cd92b109a0761");

        public static readonly UnityGuid kProfilerPlayerInfoMessage = new UnityGuid(0x49, 0x3d, 0xea, 0x75, 0xac, 0xb4, 0xe4, 0x9d, 0x48, 0x80, 0x8b, 0x72, 0x1f, 0x08, 0x58, 0xd3); //"94d3ae57ca4b4ed98408b827f180853d");
        //public static readonly UnityGuid kProfilerSetDeepProfilerModeMessage = new UnityGuid("bf0c550cd24d498cbb28380a8467622d");  // UNSUPPORTED
        //public static readonly UnityGuid kProfilerSetMarkerFiltering = new UnityGuid("18207525e148469ea059ec2cdfb026a5");
    }

    // Multicast is used to announce our existence to the local network - especially to Unity Editor. It can also be useful, for instance, for
    // debuggers to know about us.
    //
    // Multicast should always be enabled if player connection is enabled in non-web builds. Multicast's main purpose is to support
    // the editor initiating a connection to us automatically in development builds.
    //
    // However, in web builds, since there is
    // a) no UDP in WebSockets and 
    // b) no listening for WebSockets connections therefore no auto-connection from the Editor
    // we disable multicasting.
    //
    // SIDE NOTE:
    // IL2CPP managed debugging uses multicasting even on web through it is not supported by normal WebSockets. Support for this
    // is provided through a posix-sockets emulation layer by a WebSockets based proxy-server included with Emscripten. As mentioned in a later
    // comment, this manner of translation is too slow to be used for profiler, livelink, etc, and so we do not take "advantage"
    // of it for the general case here.

#if ENABLE_MULTICAST
    // This is used inside Connection directly
    internal class Multicast
    {
        private static Baselib_Socket_Handle hSocket = Baselib_Socket_Handle_Invalid;
        private static Baselib_NetworkAddress hAddress;
        private static Baselib_ErrorState errState;
        private static string broadcastIp = "225.0.0.222";
        private static ushort broadcastPort = (ushort)EditorPorts.Multicast;
        private static int broadcastCountdown = 0;
        private static bool initialized = false;
        private static string localIp = "127.0.0.1";
        private static string whoAmI;

        private const uint kPlayerConnectionVersion = 0x00100100;  // must match with editor build
        private const uint kPlayerGuidDirectConnect = 1337;  // special player id that we must provide if we aren't listening for unity editor connection request
        private const int kBroadcastCounter = 30;

        [Flags]
        private enum Flags : ushort
        {
            kRequestImmediateConnect = 1 << 0,  // must be enabled for auto connect to have effect
            kSupportsProfile = 1 << 1,
            //kCustomMessage = 1 << 2,  // unused
            //kUseAlternateIP = 1 << 3,  // unused
            kAutoConnect = 1 << 4
        };

        private static void CreateWhoAmI(bool directConnect, ushort listenPort)
        {
            Flags flags = 0;
            if (!directConnect)
                flags |= Flags.kAutoConnect | Flags.kRequestImmediateConnect;
#if ENABLE_PROFILER
            flags |= Flags.kSupportsProfile;
#endif
            uint playerGuid32 = (uint)Baselib_Timer_GetHighPrecisionTimerTicks();
            if (playerGuid32 == 0)  // id 0 is special the editor
                playerGuid32--;
            if (directConnect)
                playerGuid32 = kPlayerGuidDirectConnect;

#if UNITY_WINDOWS
            string platform = "DotsRuntimeWindowsPlayer";
#elif UNITY_LINUX
            string platform = "DotsRuntimeLinuxPlayer";
#elif UNITY_MACOSX
            string platform = "DotsRuntimeOsxPlayer";
#elif UNITY_IOS
            string platform = "DotsRuntimeIosPlayer";
#elif UNITY_ANDROID
            string platform = "DotsRuntimeAndroidPlayer";
#elif UNITY_WEBGL
            string platform = "DotsRuntimeWebglPlayer";
#else
            string platform = "DotsRuntimePlayer";
#endif

#if UNITY_DOTSPLAYER_IL2CPP_MANAGED_DEBUGGER // This is irrelevant for non-il2cpp builds
            int debugEnabled = 1;
#else
            int debugEnabled = 0;
#endif

            whoAmI = $"[IP] {localIp}";
            whoAmI += $" [Port] {listenPort}";
            whoAmI += $" [Flags] {(ushort)flags}";
            whoAmI += $" [Guid] {playerGuid32}";
            whoAmI += $" [EditorId] {0}";  // @@todo need the editor id for autoconnect
            whoAmI += $" [Version] {kPlayerConnectionVersion}";
#if UNITY_DOTSPLAYER_IL2CPP_MANAGED_DEBUGGER // This is irrelevant for non-il2cpp builds
            whoAmI += $" [Id] {platform}({localIp}):56000";
#else
            whoAmI += $" [Id] {platform}";
#endif
            whoAmI += $" [Debug] {debugEnabled}";
            whoAmI += $" [PackageName] {platform}";
            whoAmI += $" [ProjectName] {"DOTS_Runtime_Game"}";  // @@todo need a game name
        }

        public static void Initialize(bool directConnect, ushort listenPort)
        {
            if (initialized)
                return;

            unsafe
            {
                hSocket = Baselib_Socket_Create(Baselib_NetworkAddress_Family.IPv4, Baselib_Socket_Protocol.UDP, (Baselib_ErrorState*)UnsafeUtility.AddressOf(ref errState));
                if (errState.code == Baselib_ErrorCode.Success)
                {
                    fixed (byte* bip = System.Text.Encoding.UTF8.GetBytes(broadcastIp))
                    {
                        Baselib_NetworkAddress_Encode((Baselib_NetworkAddress*)UnsafeUtility.AddressOf(ref hAddress), Baselib_NetworkAddress_Family.IPv4,
                            bip, broadcastPort, (Baselib_ErrorState*)UnsafeUtility.AddressOf(ref errState));
                    }
                }
            }

            if (errState.code != Baselib_ErrorCode.Success)
            {
                if (hSocket.handle != Baselib_Socket_Handle_Invalid.handle)
                {
                    Baselib_Socket_Close(hSocket);
                    hSocket = Baselib_Socket_Handle_Invalid;
                }
                return;
            }

            CreateWhoAmI(directConnect, listenPort);

            initialized = true;
        }

        public static void Shutdown()
        {
            if (!initialized)
                return;

            if (hSocket.handle != Baselib_Socket_Handle_Invalid.handle)
            {
                Baselib_Socket_Close(hSocket);
                hSocket = Baselib_Socket_Handle_Invalid;
            }

            initialized = true;
            errState.code = Baselib_ErrorCode.Success;
        }

        public static void Broadcast(bool directConnect, ushort listenPort)
        {
            Initialize(directConnect, listenPort);

            if (!initialized)
                return;

            if (broadcastCountdown > 0)
            {
                broadcastCountdown--;
                return;
            }

            Baselib_Socket_Message message = new Baselib_Socket_Message();
            unsafe
            {
                var bytes = UTF8.GetBytes(whoAmI);
                fixed (byte* bip = bytes)
                {
                    message.data = (IntPtr)bip;
                }
                message.dataLen = (uint)bytes.Length;
                message.address = (Baselib_NetworkAddress*)UnsafeUtility.AddressOf(ref hAddress);

                Baselib_Socket_UDP_Send(hSocket, &message, 1, (Baselib_ErrorState*)UnsafeUtility.AddressOf(ref errState));
            }

            broadcastCountdown = kBroadcastCounter;
        }
    }
#endif

    public class Connection
    {
        private enum ConnectionState
        {
            Init,
            ConnectDirect,
            ConnectListenBind,
            ConnectListenListen,
            ConnectListenAccept,

            Ready,
            Invalid,
        }

        private class MessageCallback
        {
            public UnityGuid messageId;
            public event UnityAction<UnityEngine.Networking.PlayerConnection.MessageEventArgs> callbacks;

            public void Invoke(UnityEngine.Networking.PlayerConnection.MessageEventArgs args)
            {
                callbacks.Invoke(args);
            }

            public void Invoke(int playerId, byte[] data)
            {
                callbacks.Invoke(new UnityEngine.Networking.PlayerConnection.MessageEventArgs { playerId = playerId, data = data });
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MessageHeader
        {
            public uint magicId;
            public UnityGuid messageId;
            public int bytes;
        }

        private static bool serviceInitialized = false;

#if !UNITY_WEBGL
        private static Baselib_Socket_Handle hSocket = Baselib_Socket_Handle_Invalid;
        private static Baselib_Socket_Handle hSocketListen = Baselib_Socket_Handle_Invalid;
        private static Baselib_NetworkAddress hAddress;
        private static Baselib_ErrorState errState;
#endif

        private static List<MessageCallback> m_EventMessageList = new List<MessageCallback>();
        private static ConnectionState state = ConnectionState.Init;

        // - On desktop, we can auto-connect to Unity Editor because we are on the same host and don't have to guess the IP
        // - On web, we can only support auto-connect to Unity Editor so it will only work if running on the same host
        // - On mobile, we are definitively not on the same host, so we must listen for a connection from Unity Editor
        //
        // Eventually, all platforms except web will listen because we will be able to multicast to the Editor which will
        // cause it to auto-connect to us. (This is work in progress and requires an API in the editor to be exposed first
        // in order to produce the correct multicast message in the player)

#if UNITY_WINDOWS || UNITY_LINUX || UNITY_MACOSX
        private static ConnectionState initType = ConnectionState.ConnectDirect;
        private static string initIp = "127.0.0.1";  // default connect to local host
        private static ushort initPort = (ushort)EditorPorts.DirectConnect;
#elif UNITY_WEBGL
#if ENABLE_MULTICAST
        // Only needed in multicasting scenario if on WEBGL platform
        private static ConnectionState initType = ConnectionState.ConnectDirect;
#endif
        private static string initIp = "ws://127.0.0.1";  // default connect to local host
        private static ushort initPort = (ushort)EditorPorts.WebSocketProxy;
#else
        private static ConnectionState initType = ConnectionState.ConnectListenBind;
        private static string initIp = "0.0.0.0";  // default listen on all ip address
        private static ushort initPort = (ushort)EditorPorts.DirectListenFirst;
#endif
        private static int initRetryCounter = 0;

        private static MessageStream bufferReceive = new MessageStream(kReserveCapacityReceive);

        public const int kReserveCapacityReceive = 8192;
        public const int kInitRetryCounter = 30;

        public static bool ConnectionInitialized => state == ConnectionState.Ready || state == ConnectionState.Invalid;
        public static bool Connected => state == ConnectionState.Ready;
        public static bool Listening => state == ConnectionState.ConnectListenAccept;
        public static bool HasSendDataQueued => !MessageStreamManager.bufferQueue.Data.sendQueue.Empty();

#if UNITY_WINDOWS
        // WSAStartup and WSACleanup is needed for windows support currently. This will be removed
        // once socket subsystem startup/shutdown functionality is properly abstracted in platforms library.
        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct WSAData
        {
            public Int16 wVersion;
            public Int16 wHighVersion;
            public fixed byte szDescription[257];
            public fixed byte szSystemStatus[129];
            public Int16 iMaxSockets;
            public Int16 iMaxUdpDg;
            public IntPtr lpVendorInfo;
        }

        [DllImport("ws2_32.dll", CharSet = CharSet.Ansi)]
        static extern private Int32 WSAStartup(Int16 wVersionRequested, out WSAData wsaData);

        [DllImport("ws2_32.dll", CharSet = CharSet.Ansi)]
        static extern private Int32 WSACleanup();

        private static void PlatformInit()
        {
            WSAStartup(0x202, out WSAData data);
        }

        private static void PlatformShutdown()
        {
            WSACleanup();
        }

        // While baselib implements its sockets API for web using typical posix calls (berkeley sockets) as supported by Emscripten,
        // it is very slow. WebGL requires WebSockets which is a layer on top of TCP, but the proxy server which Emscripten provides
        // actually emulates the posix API from WebSockets messages implemented internally which even let's us use UDP sockets.
        //
        // Again, this is very slow, so for large amounts of data such as profiler or live link which are implemented over
        // player connection, we provide
        // A) A custom web socket wrapper API designed for use here, specifically
        // B) A customized websockify which knows about player connection messages
        //
        // The following is part A) and part B) is included with Unity Editor's WebGL module

#elif UNITY_WEBGL

        private const string DLL = "__Internal";

        [DllImport(DLL, EntryPoint = "js_html_playerconnectionPlatformInit")]
        private static extern void PlatformInit();

        [DllImport(DLL, EntryPoint = "js_html_playerconnectionPlatformShutdown")]
        private static extern void PlatformShutdown();

        [DllImport(DLL, EntryPoint = "js_html_playerconnectionConnect")]
        private static unsafe extern void WebSocketConnect(byte* serverAddress);

        [DllImport(DLL, EntryPoint = "js_html_playerconnectionDisconnect")]
        private static extern void WebSocketDisconnect();

        [DllImport(DLL, EntryPoint = "js_html_playerconnectionSend")]
        private static extern uint WebSocketSend(IntPtr data, int dataByteCount);

        [DllImport(DLL, EntryPoint = "js_html_playerconnectionReceive")]
        private static extern uint WebSocketReceive(IntPtr buffer, int reqBytes);

        [DllImport(DLL, EntryPoint = "js_html_playerconnectionLostConnection")]
        private static extern int WebSocketLostConnection();
        
        [DllImport(DLL, EntryPoint = "js_html_playerconnectionIsConnecting")]
        private static extern int WebSocketIsConnecting();

#else
        private static void PlatformInit()
        {
        }

        private static void PlatformShutdown()
        {
        }
#endif

        public static void Initialize()
        {
            if (serviceInitialized)
                return;

            PlatformInit();
            bufferReceive = new MessageStream(kReserveCapacityReceive);

            MessageStreamManager.Initialize();  // must be before initializing senders

            UnityEngine.Networking.PlayerConnection.PlayerConnection.instance.Initialize();
#if ENABLE_MULTICAST
            Multicast.Initialize(initType == ConnectionState.ConnectDirect, initPort);
#endif

            serviceInitialized = true;
        }

        public static void Shutdown()
        {
            if (!serviceInitialized)
                return;

#if ENABLE_MULTICAST
            Multicast.Shutdown();
#endif
            Disconnect();

            UnityEngine.Networking.PlayerConnection.PlayerConnection.instance.Shutdown();

            MessageStreamManager.Shutdown();

            bufferReceive.Free();
            PlatformShutdown();

            serviceInitialized = false;
        }

        public static void ConnectDirect(string forceIp, ushort forcePort)
        {
            initIp = forceIp;
            initPort = forcePort;
#if !UNITY_WEBGL
            initType = ConnectionState.ConnectDirect;
#endif
            initRetryCounter = 0;
            Connect();
        }

#if !UNITY_WEBGL
        public static void ConnectListen(string forceIp, ushort forcePort)
        {
            initIp = forceIp;
            initPort = forcePort;
            initType = ConnectionState.ConnectListenBind;
            initRetryCounter = 0;
            Connect();
        }
#endif

        public static void Connect()
        {
            if (ConnectionInitialized)
                return;

#if UNITY_WEBGL
            if (state == ConnectionState.Init)
            {
                if (initRetryCounter > 0)
                {
                    initRetryCounter--;
                    return;
                }

                unsafe
                {
                    fixed (byte* bip = System.Text.Encoding.UTF8.GetBytes(initIp + $":{initPort}"))
                    {
                        WebSocketConnect(bip);
                    }
                }

                state = ConnectionState.Ready;
            }
#else
            if (state == ConnectionState.Init)
            {
                if (initRetryCounter > 0)
                {
                    initRetryCounter--;
                    return;
                }

                unsafe
                {
                    hSocket = Baselib_Socket_Create(Baselib_NetworkAddress_Family.IPv4, Baselib_Socket_Protocol.TCP, (Baselib_ErrorState *)UnsafeUtility.AddressOf(ref errState));
                    if (errState.code == Baselib_ErrorCode.Success)
                    {
                        fixed (byte* bip = System.Text.Encoding.UTF8.GetBytes(initIp))
                        {
                            Baselib_NetworkAddress_Encode((Baselib_NetworkAddress*)UnsafeUtility.AddressOf(ref hAddress), Baselib_NetworkAddress_Family.IPv4, 
                                bip, initPort, (Baselib_ErrorState*)UnsafeUtility.AddressOf(ref errState));
                        }
                    }
                }

                if (errState.code == Baselib_ErrorCode.Success)
                    state = initType;
                else
                    state = ConnectionState.Invalid;
            }

            if (state == ConnectionState.ConnectDirect)
            {
                unsafe
                {
                    Baselib_Socket_TCP_Connect(hSocket, (Baselib_NetworkAddress*)UnsafeUtility.AddressOf(ref hAddress), Baselib_NetworkAddress_AddressReuse.Allow,
                        (Baselib_ErrorState*)UnsafeUtility.AddressOf(ref errState));
                }

                if (errState.code == Baselib_ErrorCode.Success)
                    state = ConnectionState.Ready;
                else
                    state = ConnectionState.Invalid;
            }
            else
            {
                if (state == ConnectionState.ConnectListenBind)
                {
                    unsafe
                    {
                        Baselib_Socket_Bind(hSocket, (Baselib_NetworkAddress*)UnsafeUtility.AddressOf(ref hAddress), Baselib_NetworkAddress_AddressReuse.Allow,
                            (Baselib_ErrorState*)UnsafeUtility.AddressOf(ref errState));
                    }

                    if (errState.code == Baselib_ErrorCode.Success)
                        state = ConnectionState.ConnectListenListen;
                    else
                        state = ConnectionState.Invalid;
                }

                if (state == ConnectionState.ConnectListenListen)
                {
                    unsafe
                    {
                        Baselib_Socket_TCP_Listen(hSocket, (Baselib_ErrorState*)UnsafeUtility.AddressOf(ref errState));
                    }

                    if (errState.code == Baselib_ErrorCode.Success)
                        state = ConnectionState.ConnectListenAccept;
                    else
                        state = ConnectionState.Invalid;
                }

                if (state == ConnectionState.ConnectListenAccept)
                {
                    unsafe
                    {
                        hSocketListen = Baselib_Socket_TCP_Accept(hSocket, (Baselib_ErrorState*)UnsafeUtility.AddressOf(ref errState));
                    }

                    if (errState.code != Baselib_ErrorCode.Success)
                        state = ConnectionState.Invalid;
                    else if (hSocketListen.handle != Baselib_Socket_Handle_Invalid.handle)
                    {
                        // Swap so rx/tx code works on same path
                        var hSocketTemp = hSocket;
                        hSocket = hSocketListen;
                        hSocketListen = hSocketTemp;
                        state = ConnectionState.Ready;
                    }
                }
            }

            if (state == ConnectionState.Invalid)
            {
                if (hSocket.handle != Baselib_Socket_Handle_Invalid.handle)
                {
                    Baselib_Socket_Close(hSocket);
                    hSocket = Baselib_Socket_Handle_Invalid;
                }
            }
#endif
        }

        public static void Disconnect()
        {
            initRetryCounter = 0;

            if (state == ConnectionState.Init)
                return;

#if UNITY_WEBGL
            WebSocketDisconnect();
#else
            if (hSocketListen.handle != Baselib_Socket_Handle_Invalid.handle)
            {
                Baselib_Socket_Close(hSocketListen);
                hSocketListen = Baselib_Socket_Handle_Invalid;
            }

            if (hSocket.handle != Baselib_Socket_Handle_Invalid.handle)
            {
                Baselib_Socket_Close(hSocket);
                hSocket = Baselib_Socket_Handle_Invalid;
            }

            errState.code = Baselib_ErrorCode.Success;
#endif

            state = ConnectionState.Init;

            MessageStreamManager.RecycleAll();
            bufferReceive.RecycleAndFreeExtra();
        }

        [MonoPInvokeCallback]
        public static void TransmitAndReceive()
        {
#if ENABLE_MULTICAST
            Multicast.Broadcast(initType == ConnectionState.ConnectDirect, initPort);
#endif
            Connect();

            if (!Connected)
            {
                if (state == ConnectionState.Invalid)
                    MessageStreamManager.RecycleAll();
                return;
            }

            // Check if got disconnected
#if UNITY_WEBGL
            if (WebSocketLostConnection() == 1)
#else
            Baselib_Socket_PollFd pollFd = new Baselib_Socket_PollFd();
            unsafe
            {
                pollFd.handle.handle = hSocket.handle;
                pollFd.errorState = (Baselib_ErrorState*)UnsafeUtility.AddressOf(ref errState);
                pollFd.requestedEvents = Baselib_Socket_PollEvents.Connected;

                Baselib_Socket_Poll(&pollFd, 1, 0, (Baselib_ErrorState*)UnsafeUtility.AddressOf(ref errState));
            };

            if (errState.code != Baselib_ErrorCode.Success)
#endif
            {
                Disconnect();
                return;
            }

            // Disconnection didn't occur, but we could still be waiting on a connection 
#if UNITY_WEBGL
            if (WebSocketIsConnecting() == 1)
#else
            if (pollFd.resultEvents != Baselib_Socket_PollEvents.Connected)
#endif
            {
                return;
            }

            MessageStreamManager.TrySubmitAll();
            Receive();

            if (!Connected)
                return;

            Transmit();
        }

        private static unsafe void Receive()
        {
            // Receive anything sent to us
            // Similar setup for sending data
            MessageHeader* header = (MessageHeader*)bufferReceive.BufferRead->Buffer;

            int bytesNeeded = 0;
            if (bufferReceive.TotalBytes < sizeof(MessageHeader))
                bytesNeeded = sizeof(MessageHeader) - bufferReceive.TotalBytes;
            else
                bytesNeeded = sizeof(MessageHeader) + header->bytes - bufferReceive.TotalBytes;

            while (bytesNeeded > 0)
            {
                MessageStream.MessageStreamBuffer* bufferWrite = bufferReceive.BufferWrite;

                int bytesAvail = bufferWrite->Capacity - bufferWrite->Size;
#if UNITY_WEBGL
                uint actualWritten = WebSocketReceive(bufferWrite->Buffer + bufferWrite->Size,
                    bytesNeeded <= bytesAvail ? bytesNeeded : bytesAvail);
                if (actualWritten == 0xffffffff)
#else
                uint actualWritten = Baselib_Socket_TCP_Recv(hSocket, bufferWrite->Buffer + bufferWrite->Size,
                    (uint)(bytesNeeded <= bytesAvail ? bytesNeeded : bytesAvail), (Baselib_ErrorState*)UnsafeUtility.AddressOf(ref errState));
                if (errState.code != Baselib_ErrorCode.Success)
#endif
                {
                    // Something bad happened; lost connection maybe?
                    // After cleaning up, next time we will try to re-initialize
                    Disconnect();
                    initRetryCounter = kInitRetryCounter;
                    return;
                }

                if (bytesNeeded > 0 && actualWritten == 0)
                {
                    // Don't do anything with data until we've received everything
                    return;
                }

                bufferReceive.UpdateSize((int)actualWritten);
                bytesNeeded -= (int)actualWritten;
                if (bytesNeeded == 0)
                {
                    // Finished receiving header
                    if (bufferReceive.TotalBytes == sizeof(MessageHeader))
                    {
                        // De-synced somewhere... reset connection
                        if (header->magicId != EditorMessageIds.kMagicNumber)
                        {
                            Disconnect();
                            initRetryCounter = kInitRetryCounter;
                            return;
                        }
                        bytesNeeded = header->bytes;
                        bufferReceive.Allocate(bytesNeeded);
                    }

                    // Finished receiving message
                    if (bytesNeeded == 0)
                    {
                        // Otherwise bytesNeeded becomes 0 after message is finished, which can be immediately in the
                        // case of PlayerMessageId.kApplicationQuit (message size 0)
                        foreach (var e in m_EventMessageList)
                        {
                            if (e.messageId == header->messageId)
                            {
                                // This could be anything from a 4-byte "bool" to an asset sent from the editor
                                byte[] messageData = bufferReceive.ToByteArray(sizeof(MessageHeader), bufferReceive.TotalBytes);
                                e.Invoke(0, messageData);
                            }
                        }

                        if (header->messageId == EditorMessageIds.kApplicationQuit)
                        {
                            UnityEngine.Debug.Log("Unity editor has been closed");
                            Disconnect();
                        }

                        if (header->messageId == EditorMessageIds.kNoFurtherConnections)
                        {
                            UnityEngine.Debug.Log("Unity editor can not accept any more connections");
                            Disconnect();
                        }

                        // Poll for next message
                        bufferReceive.RecycleAndFreeExtra();
                        bytesNeeded = sizeof(MessageHeader);
                    }
                }
            }

            // This code should not be executable
            throw new InvalidOperationException("Internal error receiving network data");
        }

        private static unsafe void Transmit()
        {
            // Transmit anything in buffers
            while (HasSendDataQueued)
            {
                MessageStream* bufferSend = MessageStreamManager.bufferQueue.Data.sendQueue.Pop();
                MessageStream.MessageStreamBuffer* bufferRead = bufferSend->BufferRead;
                int offset = 0;

                while (bufferRead != null)
                {
#if UNITY_WEBGL
                    uint actualRead = WebSocketSend(bufferRead->Buffer + offset, bufferRead->Size - offset);
                    if (actualRead == 0xffffffff)
#else
                    uint actualRead = Baselib_Socket_TCP_Send(hSocket, bufferRead->Buffer + offset, (uint)(bufferRead->Size - offset), (Baselib_ErrorState*)UnsafeUtility.AddressOf(ref errState));
                    if (errState.code != Baselib_ErrorCode.Success)
#endif
                    {
                        // Something bad happened; lost connection maybe?
                        // After cleaning up, next time we will try to re-initialize
                        Disconnect();
                        initRetryCounter = kInitRetryCounter;
                        return;
                    }

                    if (actualRead == 0)
                    {
                        // Move the data to be sent to the front of this buffer for next time
                        bufferSend->RecycleRange(bufferRead, offset);
                        return;
                    }

                    offset += (int)actualRead;
                    if (offset == bufferRead->Size)
                    {
                        bufferRead = bufferRead->Next;
                        offset = 0;
                    }
                }

                MessageStreamManager.RecycleBuffer(bufferSend);
            }
        }

        public static void RegisterMessage(UnityGuid messageId, UnityEngine.Events.UnityAction<UnityEngine.Networking.PlayerConnection.MessageEventArgs> callback)
        {
            GetMessageEvent(messageId).callbacks += callback;
        }

        public static void UnregisterMessage(UnityGuid messageId, UnityEngine.Events.UnityAction<UnityEngine.Networking.PlayerConnection.MessageEventArgs> callback)
        {
            GetMessageEvent(messageId).callbacks -= callback;
        }

        public static void UnregisterAllMessages()
        {
            m_EventMessageList.Clear();
        }

        private static MessageCallback GetMessageEvent(UnityGuid messageId)
        {
            foreach (var e in m_EventMessageList)
            {
                if (e.messageId == messageId)
                    return e;
            }

            var ret = new MessageCallback { messageId = messageId };
            m_EventMessageList.Add(ret);

            return ret;
        }
    }
}

#endif
