using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Tiny.Audio;
using Unity.Collections.LowLevel.Unsafe;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Detect when a car with the SpeedMultiplier component is near a BoostPad to give
    ///     it a temporary speed boost.
    /// </summary>
    [UpdateAfter(typeof(EndFramePhysicsSystem))]
    public class EnterBoostPad : SystemBase
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

        protected override void OnUpdate()
        {
            Dependency = new EnterBoostPadJob
            {
                BoostPadGroup = GetComponentDataFromEntity<BoostPad>(true),
                AudioSourceGroup = GetComponentDataFromEntity<AudioSource>(true),
                PlayerGroup = GetComponentDataFromEntity<PlayerTag>(true),
                SpeedMultiplierGroup = GetComponentDataFromEntity<SpeedMultiplier>(),
                EntityCommandBuffer = _entityCommandBufferSystem.CreateCommandBuffer()
            }.Schedule(_stepPhysicsWorldSystem.Simulation, ref _buildPhysicsWorldSystem.PhysicsWorld, Dependency);

            _entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }

        [BurstCompile]
        private struct EnterBoostPadJob : ITriggerEventsJob
        {
            [ReadOnly] public ComponentDataFromEntity<BoostPad> BoostPadGroup;
            [ReadOnly] public ComponentDataFromEntity<PlayerTag> PlayerGroup;
            public EntityCommandBuffer EntityCommandBuffer;
            [ReadOnly] public ComponentDataFromEntity<AudioSource> AudioSourceGroup;

            // Because multiple worker threads can write to this, the safety system won't normally allow this job to schedule.
            // We guarantee they will never write to the same
            // entity key, we disable safety restrictions which would normally not allow this
            [NativeDisableContainerSafetyRestriction]
            public ComponentDataFromEntity<SpeedMultiplier> SpeedMultiplierGroup;

            public Entity GetEntityFromComponentGroup<T>(Entity entityA, Entity entityB,
                ComponentDataFromEntity<T> componentGroup) where T : struct, IComponentData
            {
                if (componentGroup.HasComponent(entityA))
                    return entityA;
                if (componentGroup.HasComponent(entityB))
                    return entityB;
                return Entity.Null;
            }

            public void Execute(TriggerEvent collisionEvent)
            {
                var entityA = collisionEvent.EntityA;
                var entityB = collisionEvent.EntityB;

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
