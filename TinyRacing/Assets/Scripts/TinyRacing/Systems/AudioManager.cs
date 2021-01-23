using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Audio;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Manage the audio events according to the game state
    /// </summary>
    public class AudioManager : SystemBase
    {
        private AudioAssets audioAssets;

        protected override void OnStartRunning()
        {
            RequireSingletonForUpdate<AudioAssets>();
            audioAssets = GetSingleton<AudioAssets>();
            base.OnStartRunning();
        }

        protected override void OnUpdate()
        {
            if (!HasSingleton<Race>())
            {
                return;
            }

            var race = GetSingleton<Race>();
            if (race.IsNotStarted())
            {
                PlayAudio(audioAssets.IntroMusic);
                StopAudio(audioAssets.EndMusic);
            }

            if (race.IsInProgress())
            {
                PlayAudio(audioAssets.LevelMusic);
                PlayAudio(audioAssets.CountDown, true);
                StopAudio(audioAssets.IntroMusic);
            }

            if (race.IsFinished())
            {
                PlayAudio(audioAssets.EndMusic, true);
                StopAudio(audioAssets.LevelMusic);
            }

            // AI car engine sounds.
            Entities.ForEach((Entity entity, ref Car car, ref AudioSource audioSource) =>
            {
                if (math.abs(car.CurrentSpeed) > 5 && !car.IsEngineDestroyed)
                {
                    audioSource.volume = math.min(.5f, car.CurrentSpeed / 100.0f);
                    PlayAudio(entity);
                }
                else
                {
                    StopAudio(entity);
                }

                if (car.PlayCrashAudio)
                {
                    car.PlayCrashAudio = false;
                    PlayAudio(audioAssets.CarCrash);
                }
            }).WithStructuralChanges().Run();
        }

        private void PlayAudio(Entity entity, bool once = false)
        {
            if (once)
            {
                if (!EntityManager.HasComponent<PlayAudioOnce>(entity))
                {
                    EntityManager.AddComponent<PlayAudioOnce>(entity);
                }
                else
                {
                    return;
                }
            }

            if (!EntityManager.GetComponentData<AudioSource>(entity).isPlaying)
            {
                EntityManager.AddComponent<AudioSourceStart>(entity);
            }
        }

        private void StopAudio(Entity entity)
        {
            if (EntityManager.GetComponentData<AudioSource>(entity).isPlaying)
            {
                EntityManager.AddComponent<AudioSourceStop>(entity);
            }
        }

        public void Reset()
        {
            Entities.ForEach((Entity entity, ref AudioSource audioSource) =>
            {
                StopAudio(entity);

                if (EntityManager.HasComponent<PlayAudioOnce>(entity))
                {
                    EntityManager.RemoveComponent<PlayAudioOnce>(entity);
                }
            }).WithStructuralChanges().Run();
        }
    }
}
