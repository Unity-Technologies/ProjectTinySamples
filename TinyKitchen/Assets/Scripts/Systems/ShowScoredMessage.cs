using Unity.Entities;

namespace TinyKitchen
{
    /// <summary>
    /// Show Victory UI when the player scores
    /// </summary>
    public class ShowScoredMessage : SystemBase
    {
        // Time between the player scored and change to the next level
        private float m_CountDownTimer;

        protected override void OnStartRunning()
        {
            ResetTimer();
        }

        protected override void OnUpdate()
        {
            var game = GetSingleton<Game>();

            if (game.gameState == GameState.Scored)
            {
                if (m_CountDownTimer < 0)
                {
                    // Change to the next level and then, reset timer if time limit is reached
                    game.currentLevel++;
                    game.gameState = GameState.Initialization;

                    // Hide Victory UI
                    Entities.WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled)
                        .ForEach((ref Entity entity, in UIVictory uiVictory) =>
                        {
                            EntityManager.AddComponent<Disabled>(entity);
                        }).WithoutBurst().WithStructuralChanges().Run();

                    SetSingleton(game);
                    ResetTimer();
                }
                else
                {
                    // Show Victory UI
                    Entities.WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled)
                        .ForEach((ref Entity entity, in UIVictory uiVictory) =>
                        {
                            EntityManager.RemoveComponent<Disabled>(entity);
                        }).WithoutBurst().WithStructuralChanges().Run();

                    m_CountDownTimer -= Time.DeltaTime;
                }
            }
        }

        void ResetTimer()
        {
            var settings = GetSingleton<SettingsSingleton>();
            m_CountDownTimer = settings.changeLevelTime;
        }
    }
}