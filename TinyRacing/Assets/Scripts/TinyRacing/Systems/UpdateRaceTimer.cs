using Unity.Entities;
using Unity.Jobs;

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

            if (!race.IsRaceStarted)
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
                race.RaceTimer += Time.DeltaTime;
            else
                race.CountdownTimer -= Time.DeltaTime;

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
