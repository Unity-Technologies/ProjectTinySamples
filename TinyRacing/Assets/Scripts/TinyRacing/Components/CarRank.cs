using Unity.Entities;

namespace TinyRacing
{
    [GenerateAuthoringComponent]
    public struct CarRank : IComponentData
    {
        public int Value;

        // Time since start of race at the point when the last lap was finished.
        // Also the time the car finished the race, at the end of the race.
        public float LastLapTime;
    }
}
