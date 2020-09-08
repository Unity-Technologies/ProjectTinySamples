using Unity.Entities;

namespace Unity.TinyGems
{
    public class EndOfGameSystem : SystemBase
    {
        private const double k_WaitingTime = 2d;
        
        private GameState m_GameState = GameState.None;
        private double m_StartTime = 0d;

        protected override void OnCreate()
        {
            base.OnCreate();
            
            RequireSingletonForUpdate<GameStateData>();
            RequireSingletonForUpdate<ActiveScene>();
        }

        protected override void OnUpdate()
        {
            var gameState = GetSingleton<GameStateData>();
            if (gameState.State != GameState.GameOver)
                return;

            if (m_GameState != gameState.State)
            {
                m_StartTime = Time.ElapsedTime;
                m_GameState = gameState.State;
            }

            if (Time.ElapsedTime - m_StartTime > k_WaitingTime)
            {
                gameState.State = GameState.None;
                SetSingleton(gameState);

                var activeScene = GetSingleton<ActiveScene>();
                activeScene.Value = Scenes.Title;
                SetSingleton(activeScene);

                m_GameState = GameState.None;
            }
        }
    }
}
