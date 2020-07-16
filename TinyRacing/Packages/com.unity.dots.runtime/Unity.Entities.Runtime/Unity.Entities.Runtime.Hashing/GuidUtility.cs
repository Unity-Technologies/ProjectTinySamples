using System;
using Unity.Entities;

namespace Unity.Entities.Runtime.Hashing
{
    public static class GuidUtility
    {
        public static Guid NewGuid(byte[] data)
        {
            return new Guid(MurmurHash3.ComputeHash128(data));
        }

        public static Guid NewGuid(string data)
        {
            var bytes = new byte[data.Length];
            for (var i = 0; i < data.Length; ++i)
            {
                bytes[i] = (byte)data[i];
            }
            return NewGuid(bytes);
        }

#if !NET_DOTS
        public static Guid NewGuid(System.IO.FileInfo file)
        {
            using (var stream = new System.IO.FileStream(file.FullName, System.IO.FileMode.Open, System.IO.FileAccess.Read))
            {
                return NewGuid(MurmurHash3.ComputeHash128(stream));
            }
        }

        public static Guid NewGuid(System.IO.Stream stream)
        {
            return NewGuid(MurmurHash3.ComputeHash128(stream));
        }

#endif

        public static unsafe EntityGuid ToEntityGuid(this Guid guid)
        {
            if (guid == Guid.Empty)
            {
                return EntityGuid.Null;
            }

            var bytes = stackalloc byte[16];
            {
                var pGuid = (long*)&guid;
                var pDest = (long*)(bytes);
                pDest[0] = pGuid[0];
                pDest[1] = pGuid[1];
            }

            var entityGuid = new EntityGuid
            {
                a = ((ulong)bytes[0]) << 32 | ((ulong)bytes[1]) << 40 | ((ulong)bytes[2]) << 48 | ((ulong)bytes[3]) << 56 |
                    ((ulong)bytes[4]) << 16 | ((ulong)bytes[5]) << 24 | ((ulong)bytes[6]) << 0 | ((ulong)bytes[7]) << 8,
                b = ((ulong)bytes[8]) << 56 | ((ulong)bytes[9]) << 48 | ((ulong)bytes[10]) << 40 | ((ulong)bytes[11]) << 32 |
                    ((ulong)bytes[12]) << 24 | ((ulong)bytes[13]) << 16 | ((ulong)bytes[14]) << 8 | ((ulong)bytes[15]) << 0
            };
            return entityGuid;
        }

        public static Guid ToGuid(this EntityGuid entityGuid)
        {
            if (entityGuid == EntityGuid.Null)
            {
                return Guid.Empty;
            }

            var a = ((byte)(entityGuid.a >> 56) & 0xff) << 24
                | ((byte)(entityGuid.a >> 48) & 0xff) << 16
                | ((byte)(entityGuid.a >> 40) & 0xff) << 8
                | ((byte)(entityGuid.a >> 32) & 0xff);
            var b = (short)(entityGuid.a >> 16);
            var c = (short)entityGuid.a;
            var d = (byte)(entityGuid.b >> 56);
            var e = (byte)(entityGuid.b >> 48);
            var f = (byte)(entityGuid.b >> 40);
            var g = (byte)(entityGuid.b >> 32);
            var h = (byte)(entityGuid.b >> 24);
            var i = (byte)(entityGuid.b >> 16);
            var j = (byte)(entityGuid.b >> 8);
            var k = (byte)(entityGuid.b >> 0);

            return new Guid(a, b, c, d, e, f, g, h, i, j, k);
        }

        public static UInt32 ReverseBytes(UInt32 value)
        {
            return (value & 0x000000FFU) << 24 | (value & 0x0000FF00U) << 8 |
                (value & 0x00FF0000U) >> 8 | (value & 0xFF000000U) >> 24;
        }
    }
}
