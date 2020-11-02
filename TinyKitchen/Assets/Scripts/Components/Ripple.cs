using Unity.Entities;
using Unity.Mathematics;

namespace TinyKitchen
{
    [GenerateAuthoringComponent]
    public struct Ripple : IComponentData
    {
        public float Time;
        public float3 initialScale;
        public float maxScale;
        public float speed;
    }
}
