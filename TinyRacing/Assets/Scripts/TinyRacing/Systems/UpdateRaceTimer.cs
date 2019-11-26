using Unity.Entities;
using Unity.Jobs;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Update the countdown timer at the start of the race and the race duration timer during the race
    /// </summary>
    public class UpdateRaceTimer : JobComponentSystem
    {
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            float deltaTime = Time.DeltaTime;
            
            return Entities.ForEach((ref Race race) =>
            {
                if (!race.IsRaceStarted)
                    return;

                if (race.CountdownTimer <= 0f)
                    race.RaceTimer += deltaTime;
                else
                    race.CountdownTimer -= deltaTime;
            }).Schedule(inputDeps);
        }
    }
}