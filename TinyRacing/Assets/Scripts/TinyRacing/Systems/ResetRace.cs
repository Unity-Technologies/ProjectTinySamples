using Unity.Entities;
using Unity.Transforms;
#if UNITY_DOTSPLAYER
using Unity.Tiny.Input;
#else
using UnityEngine;
#endif

namespace TinyRacing.Systems
{
    /// <summary>
    ///     End the race and reset car positions when user presses Escape or exits the game over screen
    /// </summary>
    public class ResetRace : ComponentSystem
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<Race>();
        }

#if UNITY_DOTSPLAYER
        private InputSystem Input => World.GetExistingSystem<InputSystem>();
        private bool AnyKeyDown => Input.GetKeyDown(KeyCode.Space);
#else
        private bool AnyKeyDown => Input.anyKeyDown;
#endif

        protected override void OnUpdate()
        {
            var race = GetSingleton<Race>();
            var isGameOverResetButtonPressed = false;
            Entities.WithNone<AI>().WithAll<Car>().ForEach((ref LapProgress lapProgress) =>
            {
                var isRaceComplete = race.IsRaceStarted && lapProgress.CurrentLap > race.LapCount;
                if (isRaceComplete)
                {
                    race.GameOverTimer += Time.DeltaTime;
                    SetSingleton(race);
                }
#if UNITY_DOTSPLAYER
                var something = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) ||
                                Input.TouchCount() > 0;
                isGameOverResetButtonPressed = something && isRaceComplete && race.GameOverTimer > 2f;
#else
                isGameOverResetButtonPressed = AnyKeyDown && isRaceComplete && race.GameOverTimer > 2f;
#endif
            });

            // Return to main menu when user exits the game over menu or press Escape
            if (Input.GetKeyDown(KeyCode.Escape) || isGameOverResetButtonPressed) // TODO: Use Tiny/DOTS inputs
            {
                race.IsRaceStarted = false;
                race.GameOverTimer = 0f;
                SetSingleton(race);

                Entities.ForEach((Entity entity, ref CarDefaultState defaultState, ref Translation translation,
                    ref Rotation rotation, ref Car car, ref LapProgress lapProgress) =>
                {
                    translation.Value = defaultState.StartPosition;
                    rotation.Value = defaultState.StartRotation;

                    car.CurrentSpeed = 0f;
                    car.IsEngineDestroyed = false;

                    lapProgress.CurrentLap = 0;
                    lapProgress.CurrentControlPoint = 0;
                    lapProgress.CurrentControlPointProgress = 0f;
                });

                Entities.WithAll<Car>().ForEach((ref SpeedMultiplier speedMultiplier) =>
                {
                    speedMultiplier.RemainingTime = 0f;
                });
            }
        }
    }
}