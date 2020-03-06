using Unity.Entities;
using Unity.Jobs;

namespace Unity.TinyGems
{
    public class HudSystem : JobComponentSystem
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            
            RequireSingletonForUpdate<ActiveScene>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var activeScene = GetSingleton<ActiveScene>();
            
            var ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            var cmdBuffer = ecbSystem.CreateCommandBuffer().ToConcurrent();
            
            inputDeps = Entities
                .ForEach((
                    Entity entity, 
                    int entityInQueryIndex, 
                    in HudShowState showScene, 
                    in DynamicBuffer<HudObject> hudObjects) =>
            {
                for (var i = 0; i < hudObjects.Length; i++)
                {
                    var isVisible = activeScene.Value == showScene.Value;

                    if (isVisible)
                        cmdBuffer.RemoveComponent<Disabled>(entityInQueryIndex, hudObjects[i].Value);
                    else
                        cmdBuffer.AddComponent<Disabled>(entityInQueryIndex, hudObjects[i].Value);
                }
            }).Schedule(inputDeps);
            
            ecbSystem.AddJobHandleForProducer(inputDeps);
            
            return inputDeps;
        }
    }
}
