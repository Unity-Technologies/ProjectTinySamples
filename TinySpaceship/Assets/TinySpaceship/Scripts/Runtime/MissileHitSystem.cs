using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.U2D.Entities.Physics;

namespace Unity.Spaceship
{
    public class MissileHitSystem : ComponentSystem
    {
        private Random m_Random;
        
        protected override void OnCreate()
        {
            base.OnCreate();

            m_Random = new Random(1234);
            
            RequireSingletonForUpdate<ExplosionSpawner>();
        }
        protected override void OnUpdate()
        {
            var physicsWorldSystem = World.GetExistingSystem<PhysicsWorldSystem>();
            var physicsWorld = physicsWorldSystem.PhysicsWorld;

            var explosions = GetSingleton<ExplosionSpawner>();

            {
                var didExplode = false;
                Entities.WithAll<Missile>()
                    .ForEach((
                        Entity missileEntity, 
                        ref PhysicsColliderBlob collider, 
                        ref Translation tr, 
                        ref Rotation rot) =>
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

                            var exp = PostUpdateCommands.Instantiate(explosions.Prefab);
                            PostUpdateCommands.SetComponent(exp, new Translation { Value = tr.Value });

                            PostUpdateCommands.DestroyEntity(asteroidEntity);
                            PostUpdateCommands.DestroyEntity(missileEntity);

                            didExplode = true;
                        }
                    });

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
}