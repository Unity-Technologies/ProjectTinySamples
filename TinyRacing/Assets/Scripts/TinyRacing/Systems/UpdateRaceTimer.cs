using Unity.Entities;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Update the countdown timer at the start of the race and the race duration timer during the race
    /// </summary>
    public class UpdateRaceTimer : SystemBase
    {
        private bool RaceWasStarted;

        protected override void OnUpdate()
        {
            var race = GetSingleton<Race>();
            var player = GetSingletonEntity<Player>();
            var playerRank = GetComponent<CarRank>(player);
            if (!race.IsInProgress())
            {
                RaceWasStarted = false;
                return;
            }

            if (!RaceWasStarted)
            {
                race.RaceStartTime = Time.ElapsedTime;
                race.NumCars = CountCars();
                RaceWasStarted = true;
            }

            if (race.CountdownTimer <= 0f)
            {
                race.RaceTimer += Time.DeltaTime;
            }
            else
            {
                race.CountdownTimer -= Time.DeltaTime;
            }

            // Find either the leader's time, or the second car's time
            Entities.ForEach((Entity entity, ref CarRank rank, ref LapProgress progress) =>
            {
                // we only care if a car has completed one lap
                if (progress.CurrentLap < 2)
                {
                    return;
                }

                // then we want either the #2 car's time if we're the lead, or the lead's car if we're not
                if (playerRank.Value == 1 && rank.Value == 2 || playerRank.Value != 1 && rank.Value == 1)
                {
                    race.OthersLapTime = rank.LastLapTime;
                }
            }).WithStructuralChanges().Run();
            SetSingleton(race);
        }

        private int CountCars()
        {
            var carCount = 0;
            Entities.ForEach((ref Car car) => { carCount++; }).WithoutBurst().Run();
            return carCount;
        }
    }
}
