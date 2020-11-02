using Unity.Entities;
using Unity.Mathematics;

namespace TinyKitchen
{
    [GenerateAuthoringComponent]
    public struct UIAnimated : IComponentData
    {
        public float swayAmount;
        public float swaySpeed;
        public float3 origin;
    }
}
