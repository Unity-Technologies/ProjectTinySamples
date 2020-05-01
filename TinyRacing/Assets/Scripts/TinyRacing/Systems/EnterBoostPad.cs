using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Tiny.Audio;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Detect when a car with the SpeedMultiplier component is near a BoostPad to give
    ///     it a temporary speed boost.
    /// </summary>
    [UpdateAfter(typeof(EndFramePhysicsSystem))]
    public class EnterBoostPad : JobComponentSystem
    {
        private BuildPhysicsWorld _buildPhysicsWorldSystem;
        private EndSimulationEntityCommandBufferSystem _entityCommandBufferSystem;
        private StepPhysicsWorld _stepPhysicsWorldSystem;

        protected override void OnCreate()
        {
            _buildPhysicsWorldSystem = World.GetOrCreateSystem<BuildPhysicsWorld>();
            _stepPhysicsWorldSystem = World.GetOrCreateSystem<StepPhysicsWorld>();
            _entityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var jobHandle = new EnterBoostPadJob
            {
                BoostPadGroup = GetComponentDataFromEntity<BoostPad>(true),
                AudioSourceGroup = GetComponentDataFromEntity<AudioSource>(true),
                PlayerGroup = GetComponentDataFromEntity<PlayerTag>(true),
                SpeedMultiplierGroup = GetComponentDataFromEntity<SpeedMultiplier>(),
                EntityCommandBuffer = _entityCommandBufferSystem.CreateCommandBuffer()
            }.Schedule(_stepPhysicsWorldSystem.Simulation, ref _buildPhysicsWorldSystem.PhysicsWorld, inputDeps);

            _entityCommandBufferSystem.AddJobHandleForProducer(jobHandle);

            return jobHandle;
        }

        [BurstCompile]
        private struct EnterBoostPadJob : ITriggerEventsJob
        {
            [ReadOnly] public ComponentDataFromEntity<BoostPad> BoostPadGroup;
            [ReadOnly] public ComponentDataFromEntity<PlayerTag> PlayerGroup;
            public ComponentDataFromEntity<SpeedMultiplier> SpeedMultiplierGroup;
            public EntityCommandBuffer EntityCommandBuffer;
            [ReadOnly] public ComponentDataFromEntity<AudioSource> AudioSourceGroup;

            public Entity GetEntityFromComponentGroup<T>(Entity entityA, Entity entityB,
                ComponentDataFromEntity<T> componentGroup) where T : struct, IComponentData
            {
                if (componentGroup.Exists(entityA))
                    return entityA;
                if (componentGroup.Exists(entityB))
                    return entityB;
                return Entity.Null;
            }

            public void Execute(TriggerEvent collisionEvent)
            {
                var entityA = collisionEvent.Entities.EntityA;
                var entityB = collisionEvent.Entities.EntityB;

                var boostPadEntity = GetEntityFromComponentGroup(entityA, entityB, BoostPadGroup);
                var speedMultiplierEntity = GetEntityFromComponentGroup(entityA, entityB, SpeedMultiplierGroup);

                if (boostPadEntity != Entity.Null && speedMultiplierEntity != Entity.Null)
                {
                    var boostPadComponent = BoostPadGroup[boostPadEntity];
                    var speedMultiplierComponent = SpeedMultiplierGroup[speedMultiplierEntity];
                    speedMultiplierComponent.RemainingTime = boostPadComponent.SpeedBoostDuration;
                    speedMultiplierComponent.Multiplier = boostPadComponent.SpeedMultiplier;
                    SpeedMultiplierGroup[speedMultiplierEntity] = speedMultiplierComponent;

                    var isPlaying = AudioSourceGroup[boostPadEntity].isPlaying;
                    if (PlayerGroup.HasComponent(speedMultiplierEntity) && !isPlaying)
                        EntityCommandBuffer.AddComponent<AudioSourceStart>(boostPadEntity);
                }
            }
        }
    }
}
