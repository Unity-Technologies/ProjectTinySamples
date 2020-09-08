using Unity.Entities;

namespace Unity.Spaceship
{
    public class HudSystem : SystemBase
    {
        private GameStates m_CurrentGameState = GameStates.None;

        protected override void OnCreate()
        {
            base.OnCreate();
            
            RequireSingletonForUpdate<GameState>();
        }

        protected override void OnUpdate()
        {
            var gameState = GetSingleton<GameState>();
            if (gameState.Value == m_CurrentGameState)
                return;

            m_CurrentGameState = gameState.Value;
            
            var ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            var cmdBuffer = ecbSystem.CreateCommandBuffer().AsParallelWriter();
            
            Dependency = Entities.ForEach((Entity entity, int entityInQueryIndex, in HudShowState showState, in DynamicBuffer<HudObject> hudObjects) =>
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
            }).Schedule(Dependency);
            
            ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
