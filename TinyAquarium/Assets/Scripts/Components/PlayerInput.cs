using Unity.Entities;
using Unity.Mathematics;

namespace TinyAquarium
{
    [GenerateAuthoringComponent]
    public struct PlayerInput : IComponentData
    {
        public float2 InputAxis;
    }
}
