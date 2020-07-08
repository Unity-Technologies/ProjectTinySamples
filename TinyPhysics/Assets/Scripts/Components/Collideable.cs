using Unity.Entities;

namespace TinyPhysics
{
    [GenerateAuthoringComponent]
    public struct Collideable : IComponentData
    {
        public Entity CollisionEntity { get; set; }
    }
}
