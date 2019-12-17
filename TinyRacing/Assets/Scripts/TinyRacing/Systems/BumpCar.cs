using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Detect when the player's car bumps into an AI car to destroy their engine.
    ///     Do not use physics collisions but compare distances between cars
    /// </summary>
    public class BumpCar : JobComponentSystem
    {
        private Entity m_CarDestroyedSmoke;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<PlayerTag>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            m_CarDestroyedSmoke = GetSingleton<PrefabReferences>().carSmokeDestroyedPrefab;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var playerCarEntity = GetSingletonEntity<PlayerTag>();
            var playerCarPosition = EntityManager.GetComponentData<Translation>(playerCarEntity).Value;
            var destroyedSmoke = m_CarDestroyedSmoke;

            return Entities.WithNone<PlayerTag>().ForEach((ref Car car, ref SmokeSpawner smoke, in Translation translation) =>
            {
                var distanceSq = math.distancesq(translation.Value, playerCarPosition);
                if (distanceSq < 1f && !car.IsEngineDestroyed)
                {
                    car.IsEngineDestroyed = true;
                    car.PlayCrashAudio = true;
                    smoke.SmokePrefab = destroyedSmoke;
                }
            }).Schedule(inputDeps);
        }
    }
}