using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.Transforms;

namespace Unity.Spaceship
{
    class GameBoundsSystem : JobComponentSystem
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<GameState>();
            RequireSingletonForUpdate<Camera>();
        }
        
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var gameState = GetSingleton<GameState>();
            if (gameState.Value != GameStates.InGame)
                return inputDeps;           
            
            var camera = GetSingleton<Camera>();
            // get a world space rect of the visible
            var x = camera.aspect * camera.fov;
            var y = camera.fov;
            var worldRect = new float4(x+2, -x-2, y+2, -y-2);

            var ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            var ecb = ecbSystem.CreateCommandBuffer().ToConcurrent();
            inputDeps = Entities.
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
            }).Schedule(inputDeps);
            
            ecbSystem.AddJobHandleForProducer(inputDeps);
            return inputDeps;
        }
    }
}
