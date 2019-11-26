using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Audio;
using Unity.Transforms;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Detect when a car with the SpeedMultiplier component is near a BoostPad to give
    ///     it a temporary speed boost.
    /// </summary>
    public class EnterBoostPad : ComponentSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((Entity boostPadEntity, ref BoostPad boostPad, ref Translation boostPadTranslation,
                ref AudioSource audioSource) =>
            {
                var boostPadPosition = boostPadTranslation.Value;
                var boostPadSpeedMultiplier = boostPad.SpeedMultiplier;
                var boostPadDuration = boostPad.SpeedBoostDuration;
                var boostPadRangeSq = boostPad.Range * boostPad.Range;
                var isPlaying = audioSource.isPlaying;
                Entities.ForEach((Entity entity, ref SpeedMultiplier speedMultiplier, ref Translation carTranslation) =>
                {
                    if (math.distancesq(boostPadPosition, carTranslation.Value) < boostPadRangeSq)
                    {
                        speedMultiplier.Multiplier = boostPadSpeedMultiplier;
                        speedMultiplier.RemainingTime = boostPadDuration;
                        if (!EntityManager.HasComponent<AI>(entity) && !isPlaying)
                            PostUpdateCommands.AddComponent<AudioSourceStart>(boostPadEntity);
                    }
                });
            });
        }
    }
}