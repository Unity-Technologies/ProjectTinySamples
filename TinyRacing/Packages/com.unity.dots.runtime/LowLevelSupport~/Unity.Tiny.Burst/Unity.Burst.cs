using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace UnityEngine
{
    // Needed to support SharedStatic<> in Burst
    [StructLayout(LayoutKind.Sequential)]
    public unsafe partial struct Hash128
    {
        public Hash128(uint u32_0, uint u32_1, uint u32_2, uint u32_3)
        {
            m_u32_0 = u32_0;
            m_u32_1 = u32_1;
            m_u32_2 = u32_2;
            m_u32_3 = u32_3;
        }

        public Hash128(ulong u64_0, ulong u64_1)
        {
            var ptr0 = (uint*)&u64_0;
            var ptr1 = (uint*)&u64_1;

            m_u32_0 = *ptr0;
            m_u32_1 = *(ptr0 + 1);
            m_u32_2 = *ptr1;
            m_u32_3 = *(ptr1 + 1);
        }

        uint m_u32_0;
        uint m_u32_1;
        uint m_u32_2;
        uint m_u32_3;
    }
}

namespace Unity.Burst
{
    namespace LowLevel
    {
        public static class BurstCompilerService
        {
            // Support SharedStatic<>
            [DllImport("lib_unity_tiny_burst")]
            public static extern unsafe void* GetOrCreateSharedMemory(ref Hash128 subKey, uint sizeOf, uint alignment);
        }
    }

    //why is this not in the burst package!?
    public class BurstDiscardAttribute : Attribute{}
}