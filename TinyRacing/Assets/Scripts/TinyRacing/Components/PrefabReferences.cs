using Unity.Entities;

namespace TinyRacing
{
    public struct PrefabReferences : IComponentData
    {
        public Entity carSmokePrefab;
        public Entity carSmokeDestroyedPrefab;
    }
}
