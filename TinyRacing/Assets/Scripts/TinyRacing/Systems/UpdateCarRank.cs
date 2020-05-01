using Unity.Entities;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Update the current rank (first, second, etc) of the player's car based on their progress around the track and
    ///     current lap.
    /// </summary>
    [UpdateAfter(typeof(UpdateCarLapProgress))]
    public class UpdateCarRank : SystemBase
    {
        private Entity _playerCarEntity;
        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _playerCarEntity = GetSingletonEntity<PlayerTag>();
        }

        protected override void OnUpdate()
        {
            var playerRank = 1;
            var playerProgressValue = EntityManager.GetComponentData<LapProgress>(_playerCarEntity);

            // Get the current progress of the player's car
            // For each opponent/AI car that has a better progress value, increment the player's rank by 1
            Entities.WithAll<AI>().ForEach((ref Car car, ref LapProgress lapProgress) =>
            {
                var aiProgressValue = lapProgress.TotalProgress;
                if (aiProgressValue > playerProgressValue.TotalProgress)
                    playerRank++;
            }).WithoutBurst().Run();

            EntityManager.SetComponentData(_playerCarEntity, new CarRank {Value = playerRank});
        }
    }
}
