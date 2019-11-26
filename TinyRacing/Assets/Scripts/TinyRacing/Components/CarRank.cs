using Unity.Entities;

namespace TinyRacing
{
    [GenerateAuthoringComponent]
    public struct CarRank : IComponentData
    {
        public int Value;
    }
}