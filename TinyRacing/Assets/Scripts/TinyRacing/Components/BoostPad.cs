using Unity.Entities;

namespace TinyRacing
{
    [GenerateAuthoringComponent]
    public struct BoostPad : IComponentData
    {
        public float SpeedMultiplier;
        public float SpeedBoostDuration;
        public float Range;
    }
}