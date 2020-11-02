using Unity.Entities;
using Unity.Mathematics;

namespace TinyKitchen
{
    public struct ObstacleBufferElement : IBufferElementData
    {
        public Entity Entity;
        public float3 Position;
        public float3 Scale;
    }
}