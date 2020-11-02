using Unity.Entities;
using Unity.Tiny.Audio;
using Unity.Tiny.Rendering;

namespace TinyKitchen
{
    /// <summary>
    /// Change the food material according to his state
    /// Destroy food after a certain period of time
    /// </summary>
    public class DestroyFood : ComponentSystem
    {
        protected override void OnUpdate()
        {
            var deltaTime = Time.DeltaTime;
            var game = GetSingleton<Game>();
            var settings = GetSingleton<SettingsSingleton>();

            // Get access to dynamic audio
            var dynamicAudio = GetSingleton<AudioManager>();
            var destroySound = dynamicAudio.DestroyFoodAudio;
            var destroySoundData = EntityManager.GetComponentData<AudioSource>(destroySound);

            Entities.ForEach((Entity entity, ref FoodInstanceComponent food) =>
            {
                // Only continue if food is launch
                if (!food.isLaunched)
                    return;

                food.timer += deltaTime;

                if (food.timer > settings.transparentTime)
                {
                    // Change food material to an opaque one
                    var renderer = EntityManager.GetComponentData<MeshRenderer>(food.Child);
                    var mat = EntityManager.GetComponentData<LitMaterial>(renderer.material);
                    mat.transparent = false;
                    EntityManager.SetComponentData(renderer.material, mat);
                }

                if (food.timer > settings.maxAirtime)
                {
                    // Destroy food if his lifetime is done
                    EntityManager.DestroyEntity(entity);

                    // Reset values and game state
                    game.score = 0;
                    game.scoredLast = false;
                    game.gameState = GameState.Idle;
                    SetSingleton(game);

                    // Play audio
                    EntityManager.AddComponent<AudioSourceStart>(destroySound);
                }
            });
        }
    }
}