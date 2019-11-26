using Unity.Entities;
using Unity.Mathematics;

namespace TinyRacing
{
    public struct ControlPoints : IBufferElementData
    {
        public float3 Position;
    }
}