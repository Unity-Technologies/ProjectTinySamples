using Unity.Entities;
using Unity.Mathematics;

namespace TinyRacing
{
    [GenerateAuthoringComponent]
    public struct StoreDefaultState : IComponentData
    {
        public float3 StartPosition;
        public quaternion StartRotation;
    }
}
