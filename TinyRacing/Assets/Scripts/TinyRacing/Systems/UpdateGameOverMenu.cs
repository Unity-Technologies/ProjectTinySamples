using Unity.Entities;
#if UNITY_DOTSPLAYER
using Unity.Tiny.Audio;

#endif

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Update the race ending UI menu
    /// </summary>
    [UpdateAfter(typeof(ResetRace))]
    [UpdateAfter(typeof(UpdateMainMenu))]
    public class UpdateGameOverMenu : SystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<Race>();
        }

        protected override void OnUpdate()
        {
            var race = GetSingleton<Race>();

            var isGameOver = false;
            Entities.WithNone<AI>().ForEach((ref Car car, ref LapProgress lapProgress) =>
            {
                var raceFinished = lapProgress.CurrentLap > race.LapCount;
                isGameOver = race.IsRaceStarted && raceFinished;
            }).WithoutBurst().Run();

            SetMenuVisibility(isGameOver);
        }

        private void SetMenuVisibility(bool isVisible)
        {
            if (isVisible)
            {
#if UNITY_DOTSPLAYER
                Entities.WithAll<GameOverMenuTag, AudioSource, Disabled>().ForEach((Entity entity) =>
                {
                    EntityManager.AddComponent<AudioSourceStart>(entity);
                }).WithStructuralChanges().Run();
#endif
                Entities.WithAll<GameOverMenuTag, Disabled>().ForEach((Entity entity) =>
                {
                    EntityManager.RemoveComponent<Disabled>(entity);
                }).WithStructuralChanges().Run();
            }
            else
            {
                Entities.WithAll<GameOverMenuTag>().ForEach((Entity entity) =>
                {
                    EntityManager.AddComponent<Disabled>(entity);
                }).WithStructuralChanges().Run();
            }
        }
    }
}