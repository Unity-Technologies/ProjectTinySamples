using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Tiny.Text;

namespace TinyKitchen
{
    ///<summary>
    /// Update UI texts values and add simple motion
    ///</summary>
    public class UpdateUI : SystemBase
    {
        protected override void OnCreate()
        {
            RequireSingletonForUpdate<Game>();
            RequireSingletonForUpdate<FanComponent>();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            // Get access to UI entities
            var scoreUI = GetSingletonEntity<UIScore>();
            var fanSpeedUI = GetSingletonEntity<UIFanSpeed>();

            var game = GetSingleton<Game>();
            if (game.gameState == GameState.Initialization)
                return;

            // Set Score text value
            TextLayout.SetEntityTextRendererString(EntityManager, scoreUI, game.score.ToString());

            // Set fan force text value
            var fan = GetSingleton<FanComponent>();
            var amt = fan.fanForce;
            TextLayout.SetEntityTextRendererString(EntityManager, fanSpeedUI, amt.ToString());

            var time = (float) Time.ElapsedTime;

            // Add simple motion to the texts
            Entities.ForEach((ref Translation pos, in UIAnimated ui) =>
            {
                var t = time * ui.swaySpeed;
                var rx = noise.snoise(math.float2(t + 0));
                var ry = noise.snoise(math.float2(t + 1));
                pos.Value = ui.origin + math.float3(rx, ry, 0.0f) * ui.swayAmount;
            }).ScheduleParallel();
        }
    }
}