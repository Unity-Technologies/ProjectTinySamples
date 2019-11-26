using Unity.Entities;
using Unity.Tiny.Audio;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Play audio when the player's car destroys an AI.
    ///     Play motor sound when the player is accelerating
    /// </summary>
    internal class CarAudioSystem : ComponentSystem
    {
        protected override void OnUpdate()
        {
            Entities.WithAll<Car, AI>().ForEach((ref Car car) =>
            {
                if (car.PlayCrashAudio)
                {
                    car.PlayCrashAudio = false;
                    Entities.WithAll<AudioSource, AudioCarCrash>().ForEach(entity =>
                    {
                        PostUpdateCommands.AddComponent<AudioSourceStart>(entity);
                    });
                }
            });

            Entities.WithNone<AI>().ForEach((ref Car car, ref CarInputs carInputs) =>
            {
                var currentSpeed = car.CurrentSpeed;
                Entities.WithAll<AudioCarEngine>().ForEach((Entity entity, ref AudioSource audioSource) =>
                {
                    if (currentSpeed > 50)
                    {
                        if (!audioSource.isPlaying)
                            PostUpdateCommands.AddComponent<AudioSourceStart>(entity);
                    }
                    else
                    {
                        if (audioSource.isPlaying)
                            PostUpdateCommands.AddComponent<AudioSourceStop>(entity);
                    }
                });
            });
        }
    }
}