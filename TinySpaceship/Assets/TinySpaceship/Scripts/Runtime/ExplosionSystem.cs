using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.U2D.Entities;
using Unity.U2D.Entities.Physics;

namespace Unity.Spaceship
{
    [UpdateAfter(typeof(MissileHitSystem))]
    public unsafe class ExplosionSystem : ComponentSystem
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<ExplosionSpawner>();
        }        
        protected override void OnUpdate()
        {
            
            var explosions = GetSingleton<ExplosionSpawner>();
            var explosionsEntity = GetSingletonEntity<ExplosionSpawner>();
            var explosionSprites = EntityManager.GetBuffer<ExplosionSprite>(explosionsEntity);            
            
            var ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            var ecb = ecbSystem.CreateCommandBuffer().ToConcurrent();            
            
            Entities.ForEach((Entity entity, ref Explosion explosion) =>
            {
                explosion.Timer += Time.DeltaTime;
            });            
            
            Entities.ForEach((Entity e, ref Explosion explosion, ref SpriteRenderer sr) =>
            {
                int activeSprite = (int)(explosion.Timer / explosions.TimePerSprite);
                if (activeSprite >= explosionSprites.Length)
                {
                    PostUpdateCommands.DestroyEntity(e);
                }
                else
                {
                    sr.Sprite = explosionSprites[activeSprite].Sprite;
                }
            });
        }
    }
}