using System.Runtime.InteropServices;
using Unity.Entities.Serialization;
using Unity.Tiny.Codec;

namespace Unity.Entities.Runtime
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct SceneHeader
    {
        public const int CurrentVersion = 2;

        [FieldOffset(0)]
        public int Version;
        [FieldOffset(4)]
        public Codec Codec;
        [FieldOffset(8)]
        public int DecompressedSize;

        public void SerializeHeader(BinaryWriter writer)
        {
            writer.Write(CurrentVersion);
            writer.Write((int)Codec);
            writer.Write(DecompressedSize);
        }
    }
}
