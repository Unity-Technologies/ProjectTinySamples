using Unity.Entities;
using Unity.Mathematics;

namespace TinyKitchen
{
    [GenerateAuthoringComponent]
    public struct ScoreComponent : IComponentData
    {
        public int score;
    }
}
