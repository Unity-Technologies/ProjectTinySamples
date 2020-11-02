using TinyKitchen;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;

namespace TinyRacing.Systems
{
    /// <summary>
    /// Detect collision with environment
    /// </summary>
    [UpdateAfter(typeof(EndFramePhysicsSystem))]
    public class CollideWithEnvironment : JobComponentSystem
    {
        BuildPhysicsWorld m_BuildPhysicsWorldSystem;
        StepPhysicsWorld m_StepPhysicsWorldSystem;
        EndSimulationEntityCommandBufferSystem m_EntityCommandBufferSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_BuildPhysicsWorldSystem = World.GetOrCreateSystem<BuildPhysicsWorld>();
            m_StepPhysicsWorldSystem = World.GetOrCreateSystem<StepPhysicsWorld>();
            m_EntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            MakeBouncingSound job = new MakeBouncingSound
            {
                FoodGroup = GetComponentDataFromEntity<FoodInstanceComponent>(),
                EntityCommandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer()
            };

            JobHandle jobHandle = job.Schedule(m_StepPhysicsWorldSystem.Simulation,
                ref m_BuildPhysicsWorldSystem.PhysicsWorld, inputDeps);

            m_EntityCommandBufferSystem.AddJobHandleForProducer(jobHandle);

            return jobHandle;
        }

        struct MakeBouncingSound : ICollisionEventsJob
        {
            [ReadOnly] public ComponentDataFromEntity<FoodInstanceComponent> FoodGroup;
            public EntityCommandBuffer EntityCommandBuffer;

            public Entity GetEntityFromComponentGroup<T>(Entity entityA, Entity entityB, ComponentDataFromEntity<T> componentGroup) where T : struct, IComponentData
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

                var foodEntity = GetEntityFromComponentGroup(entityA, entityB, FoodGroup);
                if (foodEntity != Entity.Null)
                {
                    var foodInstance = FoodGroup[foodEntity];
                    if (foodInstance.isLaunched)
                    {
                        foodInstance.playBouncingAudio = true;
                        EntityCommandBuffer.SetComponent(foodEntity, foodInstance);
                    }
                }
            }
        }
    }
}