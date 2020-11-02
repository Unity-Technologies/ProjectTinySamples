using Unity.Entities;
using Unity.Tiny.Audio;

namespace TinyKitchen
{
    /// <summary>
    /// Play dynamic audio according to the game state and player actions
    /// </summary>
    public class PlayAudio : SystemBase
    {
        protected override void OnUpdate()
        {
            // Get access to the game states
            var game = GetSingleton<Game>();

            // Get access to dynamic audio
            var audioManager = GetSingleton<AudioManager>();
            var audioFlyingFoodEntity = audioManager.FlyingFoodAudio;
            var flyingFoodAudioSource = EntityManager.GetComponentData<AudioSource>(audioFlyingFoodEntity);
            var audioBouncingFoodEntity = audioManager.BouncingFoodAudio;
            var bouncingFoodAudioSource = EntityManager.GetComponentData<AudioSource>(audioBouncingFoodEntity);

            if (game.gameState == GameState.Flying)
            {
                // Play flying audio
                if (!flyingFoodAudioSource.isPlaying)
                    EntityManager.AddComponent<AudioSourceStart>(audioFlyingFoodEntity);
            }
            else
            {
                // Stop flying audio
                if (flyingFoodAudioSource.isPlaying)
                    EntityManager.AddComponent<AudioSourceStop>(audioFlyingFoodEntity);
            }

            // Play rebound audio
            Entities.ForEach((Entity entity, ref FoodInstanceComponent foodInstanceComponent) =>
            {
                if (foodInstanceComponent.playBouncingAudio && !foodInstanceComponent.hasPlayedBouncingAudio)
                {
                    foodInstanceComponent.hasPlayedBouncingAudio = true;
                    EntityManager.AddComponent<AudioSourceStart>(audioBouncingFoodEntity);
                }
            }).WithStructuralChanges().Run();
        }
    }
}