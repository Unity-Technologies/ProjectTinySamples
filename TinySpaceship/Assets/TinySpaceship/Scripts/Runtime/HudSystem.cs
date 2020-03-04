using Unity.Entities;
using Unity.Jobs;

namespace Unity.Spaceship
{
    public class HudSystem : JobComponentSystem
    {
        private GameStates m_CurrentGameState = GameStates.None;

        protected override void OnCreate()
        {
            base.OnCreate();
            
            RequireSingletonForUpdate<GameState>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var gameState = GetSingleton<GameState>();
            if (gameState.Value == m_CurrentGameState)
                return inputDeps;

            m_CurrentGameState = gameState.Value;
            
            var ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            var cmdBuffer = ecbSystem.CreateCommandBuffer().ToConcurrent();
            
            inputDeps = Entities.ForEach((Entity entity, int entityInQueryIndex, in HudShowState showState, in DynamicBuffer<HudObject> hudObjects) =>
            {
                for (var i = 0; i < hudObjects.Length; i++)
                {
                    var isVisible = gameState.Value == showState.Value;

                    if (isVisible)
                    {
                        cmdBuffer.RemoveComponent<Disabled>(entityInQueryIndex, hudObjects[i].Value);
                    }
                    else
                    {
                        cmdBuffer.AddComponent<Disabled>(entityInQueryIndex, hudObjects[i].Value);
                    }
                }
            }).Schedule(inputDeps);
            
            ecbSystem.AddJobHandleForProducer(inputDeps);
            
            return inputDeps;
        }
    }
}
