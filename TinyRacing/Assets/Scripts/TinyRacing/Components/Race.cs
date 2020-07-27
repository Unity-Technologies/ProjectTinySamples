using Unity.Entities;

namespace TinyRacing
{
    [GenerateAuthoringComponent]
    public struct Race : IComponentData
    {
        public int LapCount;
        public bool IsRaceStarted;
        public bool IsRaceFinished;
        public float CountdownTime;
        public float CountdownTimer;
        public float RaceTimer;
        public float GameOverTimer;

        // number of cars in the race
        public int NumCars;

        // absolute time race started
        public double RaceStartTime;
    }
}
