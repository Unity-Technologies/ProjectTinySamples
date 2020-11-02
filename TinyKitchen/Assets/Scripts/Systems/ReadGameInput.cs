using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny;
using Unity.Tiny.Input;

namespace TinyKitchen
{
    /// <summary>
    /// Control the spatula according to the player actions
    /// </summary>
    [AlwaysUpdateSystem]
    [UpdateBefore(typeof(UpdateSpatula))]
    public class ReadGameInput : SystemBase
    {
        // Get access to the mouse position accordingly to the screen display
        float2 PointerPos(InputSystem input)
        {
            // Screen
            var display = GetSingleton<DisplayInfo>();
            var screen = math.float2(display.framebufferWidth, display.framebufferHeight);
            var frame = math.float2(display.frameWidth, display.frameHeight);
            var touch = input.GetInputPosition();
            touch -= (frame - screen) / 2;
            // Define touch input
            touch = (touch / screen) * 2.0f - math.float2(1);
            return touch;
        }

        protected override void OnUpdate()
        {
            var settings = GetSingleton<SettingsSingleton>();
            var input = World.GetExistingSystem<InputSystem>();

            var game = GetSingleton<Game>();

            if ((game.gameState == GameState.Idle || game.gameState == GameState.Aiming) && input.GetMouseButton(0))
            {
                // Switch to aiming state
                if (game.gameState == GameState.Idle)
                {
                    game.gameState = GameState.Aiming;
                    SetSingleton(game);
                }            
                var touch = PointerPos(input);

                // Initialize the spatula properties while the player is holding the left mouse button
                Entities.ForEach((ref SpatulaComponent spatula) =>
                {
                    spatula.kinematic = true;
                    spatula.velocity = math.float2(0);
                    spatula.joy = touch * 2.0f;
                    spatula.joy.x *= settings.sensitivity;
                    spatula.joy.y = math.min(0.0f, spatula.joy.y);

                    var a = math.radians(settings.maxInputAngle);
                    var l = math.float2(-math.sin(a), -math.cos(a));
                    var r = math.float2(math.sin(a), -math.cos(a));

                    var contained = math.acos(math.dot(math.normalize(spatula.joy), math.float2(0.0f, -1.0f))) < a;

                    if (!contained)
                    {
                        var cross = math.cross(math.float3(spatula.joy.x, 0.0f, spatula.joy.y),
                            math.float3(0.0f, 0.0f, -1.0f));

                        // Left
                        if (cross.y < 0.0f)
                        {
                            spatula.joy = math.dot(l, spatula.joy) * l;
                        }
                        // Right
                        else
                        {
                            spatula.joy = math.dot(r, spatula.joy) * r;
                        }
                    }

                    var dist = math.length(spatula.joy);
                    if (dist > 1.0f) spatula.joy /= dist;
                }).ScheduleParallel();
            }
            // Check if the player has released the spatula
            else if (game.gameState == GameState.Aiming && input.GetMouseButtonUp(0))
            {
                game.gameState = GameState.Flying;

                // Initialize initial spatula velocity properties
                var direction = float3.zero;
                var strength = 0.0f;

                Entities.ForEach((Entity entity, ref SpatulaComponent spatula) =>
                {
                    direction = -math.normalize(math.float3(spatula.joy.x, 0.0f, spatula.joy.y));
                    strength = math.length(spatula.joy);
                    spatula.kinematic = false;
                }).WithoutBurst().Run();

                // Reset spatula initial velocity properties
                EntityManager.AddComponent<LaunchComponent>(game.FoodOnSpatula);
                EntityManager.SetComponentData(game.FoodOnSpatula, new LaunchComponent()
                {
                    direction = direction,
                    strength = strength,
                });

                // No more food on the spatula
                game.FoodOnSpatula = Entity.Null;
                SetSingleton(game);
            }
        }
    }
}