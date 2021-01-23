using Unity.Entities;

namespace TinyRacing
{
    [GenerateAuthoringComponent]
    public struct Race : IComponentData
    {
        public enum State
        {
            NotStarted,
            InProgress,
            Finished
        }

        private State raceState;
        public int LapCount;
        public float CountdownTime;
        public float CountdownTimer;
        public float RaceTimer;
        public float GameOverTimer;
        public float OthersLapTime;

        // number of cars in the race
        public int NumCars;

        // absolute time race started
        public double RaceStartTime;

        public bool IsInProgress()
        {
            return raceState == State.InProgress;
        }

        public bool IsFinished()
        {
            return raceState == State.Finished;
        }

        public bool IsNotStarted()
        {
            return raceState == State.NotStarted;
        }

        public void Start()
        {
            raceState = State.InProgress;
        }

        public void Finish()
        {
            raceState = State.Finished;
        }

        public void Reset()
        {
            raceState = State.NotStarted;
        }
    }
}
