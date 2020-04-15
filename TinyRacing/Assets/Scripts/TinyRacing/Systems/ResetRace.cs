using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Tiny.Particles;
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
    public class ResetRace : SystemBase
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

                var raceCompleted = isRaceComplete && race.GameOverTimer > 2f;
#if UNITY_DOTSPLAYER
                var UserInput = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) ||
                                Input.TouchCount() > 0;

                isGameOverResetButtonPressed = UserInput && raceCompleted;
#else
                isGameOverResetButtonPressed = AnyKeyDown && raceCompleted;
#endif
            }).WithoutBurst().Run();

            // Return to main menu when user exits the game over menu or press Escape
            if (Input.GetKeyDown(KeyCode.Escape) || isGameOverResetButtonPressed) // TODO: Use Tiny/DOTS inputs
            {
                race.IsRaceStarted = false;
                race.GameOverTimer = 0f;
                SetSingleton(race);

                Entities.ForEach((Entity entity, ref StoreDefaultState defaultState, ref Translation translation,
                    ref Rotation rotation) =>
                {
                    translation.Value = defaultState.StartPosition;
                    rotation.Value = defaultState.StartRotation;
                }).ScheduleParallel();

                Entities.ForEach((Entity entity, ref Translation translation,
                    ref PhysicsVelocity physicsVelocity,
                    ref Rotation rotation, ref Car car, ref LapProgress lapProgress) =>
                {
                    car.CurrentSpeed = 0f;
                    car.IsEngineDestroyed = false;

                    lapProgress.CurrentLap = 0;
                    lapProgress.CurrentControlPoint = 0;
                    lapProgress.CurrentControlPointProgress = 0f;

                    physicsVelocity.Linear = float3.zero;
                    physicsVelocity.Angular = float3.zero;
                }).ScheduleParallel();

                Entities.WithAll<Car>().ForEach((ref SpeedMultiplier speedMultiplier) =>
                {
                    speedMultiplier.RemainingTime = 0f;
                }).ScheduleParallel();
                
                Entities.ForEach((ref Smoke smoke) =>
                {
                    if (smoke.Explosion != Entity.Null)
                    { 
                        EntityManager.DestroyEntity(smoke.Explosion); 
                        smoke.Explosion = Entity.Null;
                    }
                       
                    if (EntityManager.HasComponent<Disabled>(smoke.CarSmoke))
                        EntityManager.RemoveComponent<Disabled>(smoke.CarSmoke);
                }).WithStructuralChanges().Run();
            }
        }
    }
}