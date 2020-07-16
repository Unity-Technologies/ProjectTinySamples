namespace Unity.Entities.Runtime.Hashing
{
    public static class MurmurHash3
    {
        public static unsafe uint ComputeHash32(byte[] bytes, uint seed = 0)
        {
            fixed(void* src = bytes)
            {
                return MurmurHash3_x86_32(src, bytes?.Length ?? 0, seed);
            }
        }

        public static unsafe byte[] ComputeHash128(byte[] bytes, uint seed = 0)
        {
            fixed(void* src = bytes)
            {
                return MurmurHash3_x64_128(src, bytes?.Length ?? 0, seed);
            }
        }

#if !NET_DOTS
        public static uint ComputeHash32(System.IO.Stream stream, uint seed = 0)
        {
            return MurmurHash3_x86_32(stream, seed);
        }

        public static byte[] ComputeHash128(System.IO.Stream stream, uint seed = 0)
        {
            return MurmurHash3_x64_128(stream, seed);
        }

#endif

        #region Implementation

        private static uint rotl32(uint x, byte r)
        {
            return (x << r) | (x >> (32 - r));
        }

        private static ulong rotl64(ulong x, byte r)
        {
            return (x << r) | (x >> (64 - r));
        }

        private static uint fmix32(uint h)
        {
            h ^= h >> 16;
            h *= 0x85ebca6b;
            h ^= h >> 13;
            h *= 0xc2b2ae35;
            h ^= h >> 16;
            return h;
        }

        private static ulong fmix64(ulong k)
        {
            k ^= k >> 33;
            k *= 0xff51afd7ed558ccd;
            k ^= k >> 33;
            k *= 0xc4ceb9fe1a85ec53;
            k ^= k >> 33;
            return k;
        }

        private static unsafe uint MurmurHash3_x86_32(void* key, int len, uint seed)
        {
            const uint c1 = 0xcc9e2d51;
            const uint c2 = 0x1b873593;

            uint h1 = seed;
            byte* data = (byte*)key;
            int nblocks = len / 4;

            // Body
            uint k1 = 0;
            uint* blocks = (uint*)(data + nblocks * 4);
            for (int i = -nblocks; i != 0; i++)
            {
                k1 = blocks[i];

                k1 *= c1;
                k1 = rotl32(k1, 15);
                k1 *= c2;

                h1 ^= k1;
                h1 = rotl32(h1, 13);
                h1 = h1 * 5 + 0xe6546b64;
            }

            // Tail
            k1 = 0;
            byte* tail = data + nblocks * 4;
            switch (len & 3)
            {
                case 3:
                    k1 ^= ((uint)tail[2]) << 16;
                    goto case 2;
                case 2:
                    k1 ^= ((uint)tail[1]) << 8;
                    goto case 1;
                case 1:
                    k1 ^= tail[0];
                    k1 *= c1;
                    k1 = rotl32(k1, 15);
                    k1 *= c2;
                    h1 ^= k1;
                    break;
            }
            ;

            // Finalization
            h1 ^= (uint)len;
            h1 = fmix32(h1);

            return h1;
        }

        private static unsafe byte[] MurmurHash3_x64_128(void* key, int len, uint seed)
        {
            const ulong c1 = 0x87c37b91114253d5;
            const ulong c2 = 0x4cf5ad432745937f;

            ulong h1 = seed;
            ulong h2 = seed;
            byte* data = (byte*)key;
            int nblocks = len / 16;

            // Body
            ulong k1 = 0;
            ulong k2 = 0;
            ulong* blocks = (ulong*)data;
            for (int i = 0; i < nblocks; i++)
            {
                k1 = blocks[i * 2 + 0];
                k2 = blocks[i * 2 + 1];

                k1 *= c1;
                k1 = rotl64(k1, 31);
                k1 *= c2;
                h1 ^= k1;

                h1 = rotl64(h1, 27);
                h1 += h2;
                h1 = h1 * 5 + 0x52dce729;

                k2 *= c2;
                k2 = rotl64(k2, 33);
                k2 *= c1;
                h2 ^= k2;

                h2 = rotl64(h2, 31);
                h2 += h1;
                h2 = h2 * 5 + 0x38495ab5;
            }

            // Tail
            k1 = 0;
            k2 = 0;
            byte* tail = data + nblocks * 16;
            switch (len & 15)
            {
                case 15:
                    k2 ^= ((ulong)tail[14]) << 48;
                    goto case 14;
                case 14:
                    k2 ^= ((ulong)tail[13]) << 40;
                    goto case 13;
                case 13:
                    k2 ^= ((ulong)tail[12]) << 32;
                    goto case 12;
                case 12:
                    k2 ^= ((ulong)tail[11]) << 24;
                    goto case 11;
                case 11:
                    k2 ^= ((ulong)tail[10]) << 16;
                    goto case 10;
                case 10:
                    k2 ^= ((ulong)tail[9]) << 8;
                    goto case 9;
                case 9:
                    k2 ^= ((ulong)tail[8]) << 0;
                    k2 *= c2;
                    k2 = rotl64(k2, 33);
                    k2 *= c1;
                    h2 ^= k2;
                    goto case 8;
                case 8:
                    k1 ^= ((ulong)tail[7]) << 56;
                    goto case 7;
                case 7:
                    k1 ^= ((ulong)tail[6]) << 48;
                    goto case 6;
                case 6:
                    k1 ^= ((ulong)tail[5]) << 40;
                    goto case 5;
                case 5:
                    k1 ^= ((ulong)tail[4]) << 32;
                    goto case 4;
                case 4:
                    k1 ^= ((ulong)tail[3]) << 24;
                    goto case 3;
                case 3:
                    k1 ^= ((ulong)tail[2]) << 16;
                    goto case 2;
                case 2:
                    k1 ^= ((ulong)tail[1]) << 8;
                    goto case 1;
                case 1:
                    k1 ^= ((ulong)tail[0]) << 0;
                    k1 *= c1;
                    k1 = rotl64(k1, 31);
                    k1 *= c2;
                    h1 ^= k1;
                    break;
            }
            ;

            // Finalization
            h1 ^= (ulong)len;
            h2 ^= (ulong)len;
            h1 += h2;
            h2 += h1;
            h1 = fmix64(h1);
            h2 = fmix64(h2);
            h1 += h2;
            h2 += h1;

            var result = new byte[16];
            fixed(byte* ptr = result)
            {
                ((ulong*)ptr)[0] = h1;
                ((ulong*)ptr)[1] = h2;
            }
            return result;
        }

#if !NET_DOTS
        private static uint MurmurHash3_x86_32(System.IO.Stream stream, uint seed)
        {
            const uint c1 = 0xcc9e2d51;
            const uint c2 = 0x1b873593;

            uint h1 = seed;
            uint len = 0;

            using (var reader = new System.IO.BinaryReader(stream))
            {
                byte[] chunk = reader.ReadBytes(4);
                while (chunk.Length > 0)
                {
                    uint k1 = 0;
                    switch (chunk.Length)
                    {
                        // Body
                        case 4:
                            k1 = chunk[0] | ((uint)chunk[1]) << 8 | ((uint)chunk[2]) << 16 | ((uint)chunk[3]) << 24;
                            k1 *= c1;
                            k1 = rotl32(k1, 15);
                            k1 *= c2;
                            h1 ^= k1;
                            h1 = rotl32(h1, 13);
                            h1 = h1 * 5 + 0xe6546b64;
                            break;

                        // Tail
                        case 3:
                            k1 ^= ((uint)chunk[2]) << 16;
                            goto case 2;
                        case 2:
                            k1 ^= ((uint)chunk[1]) << 8;
                            goto case 1;
                        case 1:
                            k1 ^= chunk[0];
                            k1 *= c1;
                            k1 = rotl32(k1, 15);
                            k1 *= c2;
                            h1 ^= k1;
                            break;
                    }
                    len += (uint)chunk.Length;
                    chunk = reader.ReadBytes(4);
                }
            }

            // Finalization
            h1 ^= len;
            h1 = fmix32(h1);

            return h1;
        }

        private static unsafe byte[] MurmurHash3_x64_128(System.IO.Stream stream, uint seed)
        {
            const ulong c1 = 0x87c37b91114253d5;
            const ulong c2 = 0x4cf5ad432745937f;

            ulong h1 = seed;
            ulong h2 = seed;
            ulong len = 0;

            using (var reader = new System.IO.BinaryReader(stream))
            {
                byte[] chunk = reader.ReadBytes(16);
                while (chunk.Length > 0)
                {
                    ulong k1 = 0;
                    ulong k2 = 0;
                    switch (chunk.Length)
                    {
                        // Body
                        case 16:
                            k1 = chunk[0] | ((ulong)chunk[1]) << 8 | ((ulong)chunk[2]) << 16 | ((ulong)chunk[3]) << 24 | ((ulong)chunk[4]) << 32 | ((ulong)chunk[5]) << 40 | ((ulong)chunk[6]) << 48 | ((ulong)chunk[7]) << 56;
                            k2 = chunk[8] | ((ulong)chunk[9]) << 8 | ((ulong)chunk[10]) << 16 | ((ulong)chunk[11]) << 24 | ((ulong)chunk[12]) << 32 | ((ulong)chunk[13]) << 40 | ((ulong)chunk[14]) << 48 | ((ulong)chunk[15]) << 56;
                            k1 *= c1;
                            k1 = rotl64(k1, 31);
                            k1 *= c2;
                            h1 ^= k1;
                            h1 = rotl64(h1, 27);
                            h1 += h2;
                            h1 = h1 * 5 + 0x52dce729;
                            k2 *= c2;
                            k2 = rotl64(k2, 33);
                            k2 *= c1;
                            h2 ^= k2;
                            h2 = rotl64(h2, 31);
                            h2 += h1;
                            h2 = h2 * 5 + 0x38495ab5;
                            break;

                        // Tail
                        case 15:
                            k2 ^= ((ulong)chunk[14]) << 48;
                            goto case 14;
                        case 14:
                            k2 ^= ((ulong)chunk[13]) << 40;
                            goto case 13;
                        case 13:
                            k2 ^= ((ulong)chunk[12]) << 32;
                            goto case 12;
                        case 12:
                            k2 ^= ((ulong)chunk[11]) << 24;
                            goto case 11;
                        case 11:
                            k2 ^= ((ulong)chunk[10]) << 16;
                            goto case 10;
                        case 10:
                            k2 ^= ((ulong)chunk[9]) << 8;
                            goto case 9;
                        case 9:
                            k2 ^= ((ulong)chunk[8]) << 0;
                            k2 *= c2;
                            k2 = rotl64(k2, 33);
                            k2 *= c1;
                            h2 ^= k2;
                            goto case 8;
                        case 8:
                            k1 ^= ((ulong)chunk[7]) << 56;
                            goto case 7;
                        case 7:
                            k1 ^= ((ulong)chunk[6]) << 48;
                            goto case 6;
                        case 6:
                            k1 ^= ((ulong)chunk[5]) << 40;
                            goto case 5;
                        case 5:
                            k1 ^= ((ulong)chunk[4]) << 32;
                            goto case 4;
                        case 4:
                            k1 ^= ((ulong)chunk[3]) << 24;
                            goto case 3;
                        case 3:
                            k1 ^= ((ulong)chunk[2]) << 16;
                            goto case 2;
                        case 2:
                            k1 ^= ((ulong)chunk[1]) << 8;
                            goto case 1;
                        case 1:
                            k1 ^= ((ulong)chunk[0]) << 0;
                            k1 *= c1;
                            k1 = rotl64(k1, 31);
                            k1 *= c2;
                            h1 ^= k1;
                            break;
                    }
                    len += (uint)chunk.Length;
                    chunk = reader.ReadBytes(16);
                }
            }

            // Finalization
            h1 ^= len;
            h2 ^= len;
            h1 += h2;
            h2 += h1;
            h1 = fmix64(h1);
            h2 = fmix64(h2);
            h1 += h2;
            h2 += h1;

            var result = new byte[16];
            fixed(byte* ptr = result)
            {
                ((ulong*)ptr)[0] = h1;
                ((ulong*)ptr)[1] = h2;
            }
            return result;
        }

#endif

        #endregion
    }
}
