using Unity.Entities;
using Unity.Mathematics;

namespace TinyRacing
{
    public struct CarDefaultState : IComponentData
    {
        public float3 StartPosition;
        public quaternion StartRotation;
    }
}