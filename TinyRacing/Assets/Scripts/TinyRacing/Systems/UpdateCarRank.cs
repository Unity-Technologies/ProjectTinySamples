using Unity.Entities;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Update the current rank (first, second, etc) of the player's car based on their progress around the track and
    ///     current lap.
    /// </summary>
    [UpdateAfter(typeof(UpdateCarLapProgress))]
    public class UpdateCarRank : ComponentSystem
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<Race>();
        }

        protected override void OnUpdate()
        {
            var playerRank = 1;
            var playerProgressValue = 0f;
            var playerCarEntity = Entity.Null;

            // Fetch race status. Only update player's rank if the race is started and has not ended yet
            var race = GetSingleton<Race>();
            var isRaceStarted = race.IsRaceStarted;
            var raceLapCount = race.LapCount;

            // Get the current progress of the player's car
            var updatePlayerRank = false;
            Entities.WithNone<AI>().ForEach((Entity entity, ref LapProgress lapProgress) =>
            {
                playerCarEntity = entity;
                playerProgressValue = CalculateProgressValue(ref lapProgress);

                // Do not update player rank if the race is not started or if it has ended
                updatePlayerRank = isRaceStarted && lapProgress.CurrentLap <= raceLapCount;
            });

            if (updatePlayerRank)
            {
                // For each opponent/AI car that has a better progress value, increment the player's rank by 1
                Entities.WithAll<AI>().ForEach((ref LapProgress lapProgress) =>
                {
                    var aiProgressValue = CalculateProgressValue(ref lapProgress);
                    if (aiProgressValue > playerProgressValue)
                        playerRank++;
                });

                EntityManager.SetComponentData(playerCarEntity, new CarRank {Value = playerRank});
            }
        }

        private float CalculateProgressValue(ref LapProgress lapProgress)
        {
            return lapProgress.CurrentLap * 1000f + lapProgress.CurrentControlPoint +
                   lapProgress.CurrentControlPointProgress;
        }
    }
}