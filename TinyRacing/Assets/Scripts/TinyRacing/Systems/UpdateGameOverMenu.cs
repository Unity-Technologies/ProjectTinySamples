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
    public class UpdateGameOverMenu : ComponentSystem
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
                isGameOver = race.IsRaceStarted && lapProgress.CurrentLap > race.LapCount;
            });

            SetMenuVisibility(isGameOver);
        }

        private void SetMenuVisibility(bool isVisible)
        {
            if (isVisible)
            {
#if UNITY_DOTSPLAYER
                Entities.WithAll<GameOverMenuTag, AudioSource, Disabled>().ForEach(entity =>
                {
                    PostUpdateCommands.AddComponent<AudioSourceStart>(entity);
                });
#endif
                Entities.WithAll<GameOverMenuTag, Disabled>().ForEach(entity =>
                {
                    PostUpdateCommands.RemoveComponent<Disabled>(entity);
                });
            }
            else
            {
                Entities.WithAll<GameOverMenuTag>().ForEach(entity =>
                {
                    PostUpdateCommands.AddComponent<Disabled>(entity);
                });
            }
        }
    }
}
