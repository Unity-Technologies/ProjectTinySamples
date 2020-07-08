using Unity.Entities;
using Unity.Physics;

namespace TinyPhysics
{
    [GenerateAuthoringComponent]
    public struct Proximity : IComponentData
    {
        public float maxDistance;
        public DistanceHit distanceHit;
    }
}
