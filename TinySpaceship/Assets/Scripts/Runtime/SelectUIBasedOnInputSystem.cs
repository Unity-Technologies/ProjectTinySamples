using Unity.Entities;
using Unity.Tiny.Input;
using Unity.Tiny;

namespace Unity.Spaceship
{
    public class SelectUIBasedOnInputSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var inputSystem = World.GetExistingSystem<InputSystem>();
            var isTouchSupported = inputSystem.IsTouchSupported();
            
            var ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            var cmdBuffer = ecbSystem.CreateCommandBuffer().AsParallelWriter();
            
            Dependency = Entities
                .WithAll<SpriteSelectionNotSetup>()
                .ForEach((
                    Entity entity, 
                    int entityInQueryIndex, 
                    ref DynamicBuffer<SpriteSelection> spriteSelection,
                    ref SpriteRenderer spriteRenderer) =>
                {
                    spriteRenderer.Sprite = isTouchSupported ? spriteSelection[0].Value : spriteSelection[1].Value;
                    
                    cmdBuffer.RemoveComponent<SpriteSelectionNotSetup>(entityInQueryIndex, entity);
                }).Schedule(Dependency);
            
            ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
