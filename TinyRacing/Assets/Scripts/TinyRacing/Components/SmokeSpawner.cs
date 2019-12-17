using Unity.Entities;
using Unity.Mathematics;

namespace TinyRacing
{
    [GenerateAuthoringComponent]
    public struct SmokeSpawner : IComponentData
    {
        public float SpawnInterval;
        public float3 SpawnOffset;
        public float SpawnTimer;
        public Entity SmokePrefab;
    }
}