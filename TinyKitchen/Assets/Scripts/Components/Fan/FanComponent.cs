using Unity.Entities;
using Unity.Mathematics;

namespace TinyKitchen
{
    [GenerateAuthoringComponent]
    public struct FanComponent : IComponentData
    {
        public float3 fanHeading;
        public float fanForce;
        public bool isRotating;
        public float rotationSpeed;
        public float3 initialHeading;
        public float3 finalHeading;
    }
}
