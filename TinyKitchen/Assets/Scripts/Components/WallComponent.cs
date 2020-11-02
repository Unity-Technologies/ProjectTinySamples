using Unity.Entities;
using Unity.Mathematics;

namespace TinyKitchen
{
    [GenerateAuthoringComponent]
    public struct WallComponent : IComponentData
    {
        public float2 xyMaxBoundaries; // need to confirm.
    }
}
