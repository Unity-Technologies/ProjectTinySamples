using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;

namespace TinyPhysics.Systems
{
    /// <summary>
    ///     Detect when a Collideable collides with another entity
    ///     To raise a CollisionEvent the option needs to be checked in the PhysicsShape component
    /// </summary>
    [UpdateAfter(typeof(EndFramePhysicsSystem))]
    public class CollisionSystem : SystemBase
    {
        private BuildPhysicsWorld buildPhysicsWorldSystem;
        private StepPhysicsWorld stepPhysicsWorldSystem;

        protected override void OnCreate()
        {
            base.OnCreate();

            buildPhysicsWorldSystem = World.GetOrCreateSystem<BuildPhysicsWorld>();
            stepPhysicsWorldSystem = World.GetOrCreateSystem<StepPhysicsWorld>();
        }

        protected override void OnUpdate()
        {
            Dependency = new CollideableJob
            {
                CollideableGroup = GetComponentDataFromEntity<Collideable>()
            }.Schedule(stepPhysicsWorldSystem.Simulation, ref buildPhysicsWorldSystem.PhysicsWorld, Dependency);
        }

        [BurstCompile]
        private struct CollideableJob : ICollisionEventsJob
        {
            public ComponentDataFromEntity<Collideable> CollideableGroup;

            public void UpdateCollideable(Entity entity, Entity collider)
            {
                if (CollideableGroup.HasComponent(entity))
                {
                    var collideable = CollideableGroup[entity];
                    collideable.CollisionEntity = collider;
                    CollideableGroup[entity] = collideable;
                }
            }

            public void Execute(CollisionEvent collisionEvent)
            {
                var entityA = collisionEvent.EntityA;
                var entityB = collisionEvent.EntityB;

                UpdateCollideable(entityA, entityB);
                UpdateCollideable(entityB, entityA);
            }
        }
    }
}