using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.Transforms;

namespace Unity.Spaceship
{
    public class GameBoundsSystem : SystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<GameState>();
            RequireSingletonForUpdate<Camera>();
        }
        
        protected override void OnUpdate()
        {
            var gameState = GetSingleton<GameState>();
            if (gameState.Value != GameStates.InGame)
                return;           
            
            var camera = GetSingleton<Camera>();
            // get a world space rect of the visible
            var x = camera.aspect * camera.fov;
            var y = camera.fov;
            var worldRect = new float4(x+2, -x-2, y+2, -y-2);

            var ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            var ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();
            Dependency = Entities.
                WithAny<Asteroid>().
                WithAny<Missile>().
                ForEach((Entity e, int entityInQueryIndex, ref LocalToWorld ltw) =>
            {
                var pos = ltw.Value.c3.xy;
                if (pos.x > worldRect.x || pos.x < worldRect.y ||
                    pos.y > worldRect.z || pos.y < worldRect.w)
                {
                    ecb.DestroyEntity(entityInQueryIndex, e);
                }
            }).Schedule(Dependency);
            
            ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
