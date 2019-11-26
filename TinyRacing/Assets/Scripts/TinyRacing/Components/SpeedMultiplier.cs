using Unity.Entities;

namespace TinyRacing
{
    [GenerateAuthoringComponent]
    public struct SpeedMultiplier : IComponentData
    {
        public float Multiplier;
        public float RemainingTime;
    }
}