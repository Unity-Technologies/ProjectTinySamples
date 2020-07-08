using Unity.Entities;
using Unity.Mathematics;

namespace TinyPhysics
{
    [GenerateAuthoringComponent]
    public struct Button : IComponentData
    {
        public float3 colorOn;
        public float3 colorOff;
    }
}
