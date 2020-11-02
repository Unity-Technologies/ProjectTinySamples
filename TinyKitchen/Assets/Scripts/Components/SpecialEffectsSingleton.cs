using Unity.Entities;

namespace TinyKitchen
{
    [GenerateAuthoringComponent]
    public struct SpecialEffectsSingleton : IComponentData
    {
        public Entity PotParticles;
        public Entity PotRipples;
        public float elapseTime;
        public bool hasSpawned;
    }
}
