using Unity.Entities;
using Unity.Jobs;
using Unity.Tiny.Input;
using Unity.U2D.Entities;

namespace Unity.Spaceship
{
    public class SelectUIBasedOnInputSystem : JobComponentSystem
    {
        
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var inputSystem = World.GetExistingSystem<InputSystem>();
            var isTouchSupported = inputSystem.IsTouchSupported();
            
            var ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            var cmdBuffer = ecbSystem.CreateCommandBuffer().ToConcurrent();
            
            inputDeps = Entities
                .WithAll<SpriteSelectionNotSetup>()
                .ForEach((
                    Entity entity, 
                    int entityInQueryIndex, 
                    ref DynamicBuffer<SpriteSelection> spriteSelection,
                    ref SpriteRenderer spriteRenderer) =>
                {
                    spriteRenderer.Sprite = isTouchSupported ? spriteSelection[0].Value : spriteSelection[1].Value;
                    
                    cmdBuffer.RemoveComponent<SpriteSelectionNotSetup>(entityInQueryIndex, entity);
                }).Schedule(inputDeps);
            
            ecbSystem.AddJobHandleForProducer(inputDeps);
            
            return inputDeps;
        }
    }
}
