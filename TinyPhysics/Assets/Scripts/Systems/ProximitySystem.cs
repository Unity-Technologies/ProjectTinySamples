using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace TinyPhysics.Systems
{
    /// <summary>
    ///     Find and store distance of closest entity
    ///     Use CollisionFilter of source entity to find target
    /// </summary>
    public class ProximitySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref Proximity proximity, ref Translation translation, ref PhysicsCollider collider) =>
            {
                ref PhysicsWorld physicsWorld = ref World.DefaultGameObjectInjectionWorld.GetExistingSystem<BuildPhysicsWorld>().PhysicsWorld;

                if (collider.Value.IsCreated)
                {
                    var pointDistanceInput = new PointDistanceInput
                    {
                        Position = translation.Value,
                        MaxDistance = proximity.maxDistance,
                        Filter = collider.Value.Value.Filter
                    };

                    // Assign DistanceHit data to proximiy component
                    physicsWorld.CalculateDistance(pointDistanceInput, out proximity.distanceHit);
                }
            }).WithoutBurst().Run();
        }
    }
}
