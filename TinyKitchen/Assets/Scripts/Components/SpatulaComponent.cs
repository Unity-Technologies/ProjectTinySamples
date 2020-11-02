using Unity.Entities;
using Unity.Mathematics;

namespace TinyKitchen
{
    public struct SpatulaComponent : IComponentData
    {
        // Spatula parts
        public Entity tip;
        public Entity mid;
        public Entity pin;

        // Spatula specs
        public float len;
        public float snap;
        public float deadzone;
        public float friction;
        public float bend;
        public bool bendPin;
        public float3 spatulaPos;

        // Spatula physics
        public float2 joy;
        public float2 velocity;
        public bool kinematic;
    }
}
