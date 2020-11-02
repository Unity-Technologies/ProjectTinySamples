using Unity.Entities;
using Unity.Tiny;

namespace Unity.Spaceship
{
    [UpdateAfter(typeof(MissileHitSystem))]
    public class ExplosionSystem : SystemBase
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
            var ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();

            var deltaTime = Time.DeltaTime;
            Dependency = Entities.ForEach((ref Explosion explosion) =>
            {
                explosion.Timer += deltaTime;
            }).Schedule(Dependency);            
            
            Dependency = Entities
                .WithReadOnly(explosionSprites)
                .ForEach((
                    Entity e, 
                    int entityInQueryIndex,
                    ref SpriteRenderer sr,
                    in Explosion explosion) =>
            {
                var activeSprite = (int)(explosion.Timer / explosions.TimePerSprite);
                if (activeSprite >= explosionSprites.Length)
                {
                    ecb.DestroyEntity(entityInQueryIndex, e);
                }
                else
                {
                    sr.Sprite = explosionSprites[activeSprite].Sprite;
                }
            }).Schedule(Dependency);
            
            ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}