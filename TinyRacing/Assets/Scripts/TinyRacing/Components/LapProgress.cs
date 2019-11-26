using Unity.Entities;

namespace TinyRacing
{
    [GenerateAuthoringComponent]
    public struct LapProgress : IComponentData
    {
        public int CurrentLap;
        public int CurrentControlPoint;
        public float CurrentControlPointProgress;
    }
}