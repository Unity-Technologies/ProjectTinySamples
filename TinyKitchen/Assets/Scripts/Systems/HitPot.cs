using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Tiny.Audio;

namespace TinyKitchen
{
    ///<summary>
    /// Check if the food has entered the pot hole
    ///</summary>
    public class HitPot : SystemBase
    {
        protected override void OnUpdate()
        {
            var game = GetSingleton<Game>();

            // Get access to dynamic sound
            var dynamicAudio = GetSingleton<AudioManager>();
            var touchPotSound = dynamicAudio.TouchPotAudio;
            var touchPotSoundData = EntityManager.GetComponentData<AudioSource>(touchPotSound);
            var successSound = dynamicAudio.SuccesAudio;
            var successSoundData = EntityManager.GetComponentData<AudioSource>(successSound);

            var settings = GetSingleton<SettingsSingleton>();
            Entity potEntity = EntityManager.CreateEntityQuery(typeof(PotComponent)).GetSingletonEntity();

            // Get access to pot transform datas
            var potPosition = EntityManager.GetComponentData<Translation>(potEntity);
            var potRotation = EntityManager.GetComponentData<Rotation>(potEntity);
            var potSize = EntityManager.GetComponentData<NonUniformScale>(potEntity);

            Entities.ForEach((Entity entity, ref Translation pos, in FoodInstanceComponent food) =>
            {
                // Define position of the food
                var diff = pos.Value - potPosition.Value;
                var normal = math.mul(potRotation.Value, math.up());
                var dist = math.dot(normal, diff);
                var proj = pos.Value - dist * normal;
                diff = proj - potPosition.Value;

                // Define properties of the pot hole
                var bounds = math.float2(potSize.Value.x, potSize.Value.z) * 0.5f;
                var contain = math.length(diff) < bounds.x - settings.potEdgeThickness;
                contain = contain && math.abs(dist) < bounds.y * 0.5f;


                // check if the food is in the pot
                if (contain)
                {
                    // Play audio
                    EntityManager.AddComponent<AudioSourceStart>(touchPotSound);
                    EntityManager.AddComponent<AudioSourceStart>(successSound);

                    // Destroy food
                    EntityManager.DestroyEntity(entity);

                    // Set new game values
                    ++game.score;
                    game.scoredLast = true;
                    game.gameState = GameState.Scored;
                    SetSingleton(game);
                }
            }).WithStructuralChanges().WithoutBurst().Run();
        }
    }
}