using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Audio;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Play audio when the player's car destroys an AI.
    ///     Play motor sound when the player is accelerating
    /// </summary>
    internal class CarAudioSystem : SystemBase
    {
        private Entity _audioCarCrashEntity;
        private Entity _audioCarEngineEntity;
        private AudioSource _carEngineAudioSource;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<AudioCarCrash>();
            RequireSingletonForUpdate<AudioCarEngine>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _audioCarCrashEntity = GetSingletonEntity<AudioCarCrash>();
            _audioCarEngineEntity = GetSingletonEntity<AudioCarEngine>();
        }

        protected override void OnUpdate()
        {
            _carEngineAudioSource = EntityManager.GetComponentData<AudioSource>(_audioCarEngineEntity);
            Entities.WithAll<AI>().ForEach((ref Car car) =>
            {
                if (car.PlayCrashAudio)
                {
                    car.PlayCrashAudio = false;
                    EntityManager.AddComponent<AudioSourceStart>(_audioCarCrashEntity);
                }
            }).WithStructuralChanges().Run();

            Entities.WithNone<AI>().ForEach((ref Car car, ref PlayerTag playerTag) =>
            {
                var currentSpeed = car.CurrentSpeed;
                if (math.abs(currentSpeed) > 5)
                {
                    _carEngineAudioSource.volume = math.min(.8f, currentSpeed / 100.0f);
                    EntityManager.SetComponentData(_audioCarEngineEntity, _carEngineAudioSource);
                    if (!_carEngineAudioSource.isPlaying)
                        EntityManager.AddComponent<AudioSourceStart>(_audioCarEngineEntity);
                }
                else
                {
                    if (_carEngineAudioSource.isPlaying)
                        EntityManager.AddComponent<AudioSourceStop>(_audioCarEngineEntity);
                }
            }).WithStructuralChanges().Run();

            // AI car engine sounds.
            Entities.ForEach((Entity entity, ref Car car, ref AudioAICarEngine engine, ref AudioSource audioSource) =>
            {
                var currentSpeed = car.CurrentSpeed;
                if ((currentSpeed > 5) && !car.IsEngineDestroyed)
                {
                    audioSource.volume = math.min(.5f, currentSpeed / 100.0f);
                    if (!audioSource.isPlaying)
                        EntityManager.AddComponent<AudioSourceStart>(entity);
                }
                else
                {
                    if (audioSource.isPlaying)
                        EntityManager.AddComponent<AudioSourceStop>(entity);
                }
            }).WithStructuralChanges().Run();
        }
    }
}
