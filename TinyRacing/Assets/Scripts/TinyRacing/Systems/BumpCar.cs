using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Detect when the player's car bumps into an AI car to destroy their engine.
    /// </summary>
    [UpdateAfter(typeof(EndFramePhysicsSystem))]
    public class BumpCar : SystemBase
    {
        private BuildPhysicsWorld _buildPhysicsWorldSystem;
        private StepPhysicsWorld _stepPhysicsWorldSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<PlayerTag>();
            _buildPhysicsWorldSystem = World.GetOrCreateSystem<BuildPhysicsWorld>();
            _stepPhysicsWorldSystem = World.GetOrCreateSystem<StepPhysicsWorld>();
        }

        protected override void OnUpdate()
        {
            Dependency = new BumpCarJob
            {
                AIGroup = GetComponentDataFromEntity<AI>(true),
                CarGroup = GetComponentDataFromEntity<Car>(),
                PlayerGroup = GetComponentDataFromEntity<PlayerTag>()
            }.Schedule(_stepPhysicsWorldSystem.Simulation, ref _buildPhysicsWorldSystem.PhysicsWorld, Dependency);
        }

        [BurstCompile]
        private struct BumpCarJob : ICollisionEventsJob
        {
            [ReadOnly] public ComponentDataFromEntity<AI> AIGroup;
            public ComponentDataFromEntity<Car> CarGroup;
            [ReadOnly] public ComponentDataFromEntity<PlayerTag> PlayerGroup;

            public Entity GetEntityFromComponentGroup<T>(Entity entityA, Entity entityB,
                ComponentDataFromEntity<T> componentGroup) where T : struct, IComponentData
            {
                if (componentGroup.HasComponent(entityA))
                    return entityA;
                if (componentGroup.HasComponent(entityB))
                    return entityB;
                return Entity.Null;
            }

            public void Execute(CollisionEvent collisionEvent)
            {
                var entityA = collisionEvent.EntityA;
                var entityB = collisionEvent.EntityB;
                var playerEntity = GetEntityFromComponentGroup(entityA, entityB, PlayerGroup);
                var aiEntity = GetEntityFromComponentGroup(entityA, entityB, AIGroup);
                if (playerEntity != Entity.Null && aiEntity != Entity.Null)
                {
                    var car = CarGroup[aiEntity];
                    if (!car.IsEngineDestroyed)
                    {
                        car.IsEngineDestroyed = true;
                        car.PlayCrashAudio = true;
                    }

                    CarGroup[aiEntity] = car;
                }
            }
        }
    }
}
