using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Tiny.Particles;
using Unity.Transforms;
using Unity.Scenes;
#if UNITY_DOTSRUNTIME
using Unity.Tiny.Input;

#else
using UnityEngine;
#endif

namespace TinyRacing.Systems
{
    /// <summary>
    ///     End the race and reset car positions when user presses Escape or exits the game over screen
    /// </summary>
    [UpdateInGroup(typeof(SceneSystemGroup))]
    [UpdateBefore(typeof(SceneSystem))]
    public class ResetRace : SystemBase
    {
#if UNITY_DOTSRUNTIME
        private InputSystem Input => World.GetExistingSystem<InputSystem>();
        private bool AnyKeyDown => Input.GetKeyDown(KeyCode.Space);
#else
        private bool AnyKeyDown => Input.anyKeyDown;
#endif

        protected override void OnUpdate()
        {
            if (!HasSingleton<Race>())
                return;

            var sceneSystem = World.GetExistingSystem<SceneSystem>();
            var race = GetSingleton<Race>();
            var isGameOverResetButtonPressed = false;
            Entities.WithNone<AI>().WithAll<Car>().ForEach((ref LapProgress lapProgress) =>
            {
                var isRaceComplete = race.IsRaceStarted && lapProgress.CurrentLap > race.LapCount;
                if (isRaceComplete)
                {
                    race.GameOverTimer += Time.DeltaTime;
                    race.IsRaceFinished = true;
                    SetSingleton(race);
                }

                var raceCompleted = isRaceComplete && race.GameOverTimer > 2f;
#if UNITY_DOTSRUNTIME
                var UserInput = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) ||
                    Input.TouchCount() > 0;

                isGameOverResetButtonPressed = UserInput && raceCompleted;
#else
                isGameOverResetButtonPressed = AnyKeyDown && raceCompleted;
#endif
            }).WithoutBurst().Run();


            if(race.IsRaceFinished)
            {
                var endingSceneEntity = GetSingletonEntity<EndingScene>();
                var endingScene = EntityManager.GetComponentData<SceneReference>(endingSceneEntity);
                sceneSystem.LoadSceneAsync(endingScene.SceneGUID, new SceneSystem.LoadParameters() { AutoLoad = true });
            }

            // Return to main menu when user exits the game over menu or press Escape
            if (Input.GetKeyDown(KeyCode.Escape) || isGameOverResetButtonPressed) // TODO: Use Tiny/DOTS inputs
            {
                // Rendering seems to hold on to entities so this breaks
                var endingSceneEntity = GetSingletonEntity<EndingScene>();
                sceneSystem.UnloadScene(endingSceneEntity);

                race.IsRaceStarted = false;
                race.IsRaceFinished = false;
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
                        lapProgress.TotalProgress = 0;
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
