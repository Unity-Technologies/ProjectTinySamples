using Unity.Entities;
using Unity.Mathematics;

namespace TinyPhysics
{
    [GenerateAuthoringComponent]
    public struct Moveable : IComponentData
    {
        public float moveForce;
        public float3 moveDirection;
    }
}
