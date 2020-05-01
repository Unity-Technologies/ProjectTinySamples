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
    }
}
