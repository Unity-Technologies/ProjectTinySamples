using Unity.Entities;
using Unity.Tiny.Input;

namespace Unity.TinyGems
{
    public class RestartSystem : SystemBase
    {
        private EntityQuery m_CellsQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<GameStateData>();
            RequireSingletonForUpdate<ActiveScene>();

            m_CellsQuery = GetEntityQuery(ComponentType.ReadWrite<Cell>());
        }

        protected override void OnUpdate()
        {
            var activeScene = GetSingleton<ActiveScene>();
            var gameState = GetSingleton<GameStateData>();
            var input = World.GetExistingSystem<InputSystem>();

            if (activeScene.Value == Scenes.Title 
                && InputUtil.GetInputDown(input))
            {
                EntityManager.DestroyEntity(m_CellsQuery);

                AudioUtils.PlaySound(EntityManager, AudioTypes.StartGame);              

                activeScene.Value = Scenes.Game;
                SetSingleton(activeScene);
                
                gameState.State = GameState.Spawn;
                gameState.Hearts = 3;
                SetSingleton(gameState);
            }
        }
    }
}
