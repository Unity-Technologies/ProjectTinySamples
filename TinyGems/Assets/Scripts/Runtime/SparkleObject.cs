using Unity.Entities;

namespace Unity.TinyGems
{
    [GenerateAuthoringComponent]
    public struct SparkleObject : IComponentData
    {
        public float StartTime { get; set; }
        public float Delay { get; set; }
    }
}
