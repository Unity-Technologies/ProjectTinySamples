using System;

namespace Unity.Baselib.LowLevel
{
    internal static partial class Binding
    {
        public static readonly Baselib_Memory_PageAllocation Baselib_Memory_PageAllocation_Invalid = new Baselib_Memory_PageAllocation();
        public static readonly Baselib_RegisteredNetwork_Socket_UDP Baselib_RegisteredNetwork_Socket_UDP_Invalid = new Baselib_RegisteredNetwork_Socket_UDP();
        public static readonly Baselib_Socket_Handle Baselib_Socket_Handle_Invalid = new Baselib_Socket_Handle { handle = (IntPtr)(-1) };
    }
}
