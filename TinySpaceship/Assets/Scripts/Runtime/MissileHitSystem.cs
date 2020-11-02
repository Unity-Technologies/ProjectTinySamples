using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.U2D.Entities.Physics;

namespace Unity.Spaceship
{
    public class MissileHitSystem : SystemBase
    {
        private Random m_Random;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<ExplosionSpawner>();
            
            m_Random = new Random(1234);
        }

        protected override void OnUpdate()
        {
            var physicsWorldSystem = World.GetExistingSystem<PhysicsWorldSystem>();
            var physicsWorld = physicsWorldSystem.PhysicsWorld;

            var explosions = GetSingleton<ExplosionSpawner>();

            var cmdBuffer = new EntityCommandBuffer(Allocator.TempJob);

            var didExplode = false;
             Entities.WithAll<Missile>()
                .ForEach((
                    Entity missileEntity,
                    in PhysicsColliderBlob collider,
                    in Translation tr,
                    in Rotation rot) =>
                {
                    // check with missile
                    if (physicsWorld.OverlapCollider(
                        new OverlapColliderInput
                        {
                            Collider = collider.Collider,
                            Transform = new PhysicsTransform(tr.Value, rot.Value),
                            Filter = collider.Collider.Value.Filter
                        },
                        out OverlapColliderHit hit))
                    {
                        var asteroidEntity = physicsWorld.AllBodies[hit.PhysicsBodyIndex].Entity;

                        var exp = cmdBuffer.Instantiate(explosions.Prefab);
                        cmdBuffer.SetComponent(exp, new Translation { Value = tr.Value });

                        cmdBuffer.DestroyEntity(asteroidEntity);
                        cmdBuffer.DestroyEntity(missileEntity);

                        didExplode = true;
                    }
                }).Run();
             
            cmdBuffer.Playback(EntityManager);
            cmdBuffer.Dispose();
            
            if (didExplode)
            {
                var randomSfx = m_Random.NextInt(0, 3);
                var explosionSfx = randomSfx == 2 ? AudioTypes.AsteroidExplosionLarge :
                    randomSfx == 1 ? AudioTypes.AsteroidExplosionMedium :
                    AudioTypes.AsteroidExplosionSmall;

                AudioUtils.PlaySound(EntityManager, explosionSfx);
            }
        }
    }
}