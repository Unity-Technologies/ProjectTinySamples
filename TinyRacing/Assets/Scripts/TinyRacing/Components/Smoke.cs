using Unity.Entities;

namespace TinyRacing
{
    [GenerateAuthoringComponent]
    public struct Smoke : IComponentData
    {
        public Entity CarSmoke;
        public Entity Explosion;
        public Entity ExplosionPrefab;
    }
}
