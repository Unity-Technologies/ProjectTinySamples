using Unity.Entities;
using Unity.Mathematics;

namespace TinyKitchen
{
    public struct LaunchComponent : IComponentData
    {
        public float3 direction;
        public float strength;
    }
}