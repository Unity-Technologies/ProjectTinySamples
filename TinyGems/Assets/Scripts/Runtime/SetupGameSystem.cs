using Unity.Entities;

namespace Unity.TinyGems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public class SetupGameSystem : SystemBase
    {
        protected override void OnStartRunning()
        {
            var gameSystemEntity = EntityManager.CreateEntity();
            
#if UNITY_EDITOR            
            EntityManager.SetName(gameSystemEntity, "GameManager");
#endif

            EntityManager.AddComponentData(gameSystemEntity, new ActiveScene()
            {
                Value = Scenes.Title
            });
            
            EntityManager.AddComponentData(gameSystemEntity, new GameStateData
            {
                State = GameState.None
            });
            
            EntityManager.CreateEntity(typeof(ActiveInput));
        }

        protected override void OnUpdate()
        {
        }
    }
}
