using Unity.Entities;

namespace TinyRacing
{
    [GenerateAuthoringComponent]
    public struct AI : IComponentData
    {
        public float NormalDistanceFromTrack;
    }
}