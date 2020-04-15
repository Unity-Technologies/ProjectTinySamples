using System;
using Unity.Entities;
using Unity.Transforms;
#if UNITY_DOTSPLAYER
using Unity.Tiny;
using Unity.Tiny.Input;
using Unity.Tiny.Audio;

#else
using UnityEngine;
#endif

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Update the main menu UI
    /// </summary>
    [UpdateBefore(typeof(ResetRace))]
    public class UpdateMainMenu : SystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();
#if UNITY_DOTSPLAYER
            var window = World.GetExistingSystem<WindowSystem>();
            window.SetOrientationMask(ScreenOrientation.AutoRotationLandscape);
#endif
            RequireSingletonForUpdate<Race>();
        }

        protected override void OnUpdate()
        {
            // Hide main menu when user presses any key
#if !UNITY_DOTSPLAYER
            bool startRaceButtonPressed = Input.anyKeyDown;
#else
            var Input = World.GetExistingSystem<InputSystem>();
            var startRaceButtonPressed =
                Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) || Input.TouchCount() > 0;
#endif

            var race = GetSingleton<Race>();
            if (startRaceButtonPressed && !race.IsRaceStarted)
            {
                Console.WriteLine("Starting race");
                race.IsRaceStarted = true;
                race.CountdownTimer = race.CountdownTime;
                race.RaceTimer = 0f;
                SetSingleton(race);
                StoreDefaultState();
            }

            SetMenuVisibility(!race.IsRaceStarted);
        }

        private void StoreDefaultState()
        {
            Entities.ForEach(
                (ref StoreDefaultState defaultState, ref Translation translation,
                    ref Rotation rotation) =>
                {
                    defaultState.StartPosition = translation.Value;
                    defaultState.StartRotation = rotation.Value;
                }).Run();
        }

        private void SetMenuVisibility(bool isVisible)
        {
            if (isVisible)
            {
#if UNITY_DOTSPLAYER
                Entities.WithAll<MainMenuTag, AudioSource, Disabled>().ForEach((Entity entity) =>
                {
                    EntityManager.AddComponent<AudioSourceStart>(entity);
                }).WithStructuralChanges().Run();
#endif
                Entities.WithAll<MainMenuTag, Disabled>().ForEach((Entity entity) =>
                {
                    EntityManager.RemoveComponent<Disabled>(entity);
                }).WithStructuralChanges().Run();
            }
            else
            {
                Entities.WithAll<MainMenuTag>()
                    .ForEach((Entity entity) => { EntityManager.AddComponent<Disabled>(entity); })
                    .WithStructuralChanges().Run();
            }
        }
    }
}