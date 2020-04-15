using Unity.Entities;
using Unity.Mathematics;

namespace TinyAquarium
{
    [GenerateAuthoringComponent]
    public struct Player : IComponentData
    {
        public float Speed;
        public float2 HorizontalLimit;
        public float2 VerticalLimit;
    }
}
