using System;
using Unity.Entities;
#if UNITY_DOTSPLAYER
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
    public class UpdateMainMenu : ComponentSystem
    {
        protected override void OnCreate()
        {
            base.OnCreate();
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
            }

            SetMenuVisibility(!race.IsRaceStarted);
        }

        private void SetMenuVisibility(bool isVisible)
        {
            if (isVisible)
            {
                Entities.WithAll<MainMenuTag, Disabled>().ForEach(entity =>
                {
                    PostUpdateCommands.RemoveComponent<Disabled>(entity);
                });
#if UNITY_DOTSPLAYER
                Entities.WithAll<MainMenuTag, AudioSource, Disabled>().ForEach(entity =>
                {
                    PostUpdateCommands.AddComponent<AudioSourceStart>(entity);
                });
#endif
            }
            else
            {
                Entities.WithAll<MainMenuTag>()
                    .ForEach(entity => { PostUpdateCommands.AddComponent<Disabled>(entity); });
            }
        }
    }
}