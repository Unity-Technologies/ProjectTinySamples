using Unity.Entities;
using Unity.Transforms;

namespace TinyKitchen
{
    /// <summary>
    /// Initialize the level based on the LevelConfiguration Data
    /// </summary>
    [UpdateBefore(typeof(HitPot))]
    [AlwaysUpdateSystem]
    public class UpdateLevelSystem : SystemBase
    {
        protected override void OnStartRunning()
        {
            // Set initial position to dynamic UI
            Entities.ForEach((ref UIAnimated ui, in Translation pos) => { ui.origin = pos.Value; }).ScheduleParallel();
        }

        protected override void OnUpdate()
        {
            var game = GetSingleton<Game>();
            if (game.gameState == GameState.Initialization)
            {
                LevelBufferElement currentLevelElement = default;
                var currentLevel = game.currentLevel;

                // Set current level configurations
                Entities.ForEach((DynamicBuffer<LevelBufferElement> buffer) =>
                {
                    currentLevelElement = buffer[currentLevel % buffer.Length];
                }).Run();

                // Set the Fan position and Fan impact
                Entities.ForEach((ref Translation translation, ref FanComponent fan) =>
                {
                    translation.Value = currentLevelElement.fanPosition;
                    fan.fanHeading = currentLevelElement.fanHeading;
                    fan.fanForce = currentLevelElement.fanForce;
                }).Run();

                // Set the Fan UI position
                Entities.ForEach((ref Translation translation, ref UIAnimated ui, in UIFanSpeed fan) =>
                {
                    translation.Value = ui.origin = currentLevelElement.fanUIPos;
                }).ScheduleParallel();

                // After initialization, set game state to gameplay
                game.gameState = GameState.Idle;
                SetSingleton(game);
            }
        }
    }
}