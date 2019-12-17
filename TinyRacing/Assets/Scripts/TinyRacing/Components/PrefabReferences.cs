using Unity.Entities;

namespace TinyRacing
{
    [GenerateAuthoringComponent]
    public struct PrefabReferences : IComponentData
    {
        public Entity carSmokePrefab;
        public Entity carSmokeDestroyedPrefab;
    }
}
