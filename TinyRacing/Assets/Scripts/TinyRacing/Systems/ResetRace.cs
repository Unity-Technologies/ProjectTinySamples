using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Scenes;
using Unity.Tiny.Input;
using Unity.Tiny.UI;
using Unity.Transforms;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     End the race and reset car positions when user presses Escape or exits the game over screen
    /// </summary>
    [UpdateInGroup(typeof(SceneSystemGroup))]
    [UpdateBefore(typeof(SceneSystem))]
    public class ResetRace : SystemBase
    {
        private InputSystem Input => World.GetExistingSystem<InputSystem>();

        protected override void OnUpdate()
        {
            if (!HasSingleton<Race>())
            {
                return;
            }

            var sceneSystem = World.GetExistingSystem<SceneSystem>();
            var race = GetSingleton<Race>();
            var isGameOverResetButtonPressed = false;

            Entities.WithAll<Player, Car>().ForEach((ref LapProgress lapProgress) =>
            {
                var isRaceComplete = race.IsInProgress() && lapProgress.CurrentLap > race.LapCount;
                if (isRaceComplete)
                {
                    race.GameOverTimer += Time.DeltaTime;
                    race.Finish();
                    SetSingleton(race);
                }
            }).WithoutBurst().Run();

            var BackButtonEntity = World.GetExistingSystem<ProcessUIEvents>().GetEntityByUIName("BackButton");
            // find if something was clicked:
            var eClicked = Entity.Null;
            Entities.ForEach((Entity e, in UIState state) =>
            {
                if (state.IsClicked)
                {
                    eClicked = e;
                }
            }).Run();
            if (eClicked != null)
            {
                if (BackButtonEntity == eClicked)
                {
                    isGameOverResetButtonPressed = true;
                }
            }

            if (race.IsFinished())
            {
                var endingSceneEntity = GetSingletonEntity<EndingScene>();
                var endingScene = EntityManager.GetComponentData<SceneReference>(endingSceneEntity);
                sceneSystem.LoadSceneAsync(endingScene.SceneGUID, new SceneSystem.LoadParameters {AutoLoad = true});
            }

            // Return to main menu when user exits the game over menu or press Escape
            if (!race.IsNotStarted() && (Input.GetKeyDown(KeyCode.Escape) || isGameOverResetButtonPressed))
            {
                // Rendering seems to hold on to entities so this breaks
                var endingSceneEntity = GetSingletonEntity<EndingScene>();
                sceneSystem.UnloadScene(endingSceneEntity);
                var audioManager = World.GetExistingSystem<AudioManager>();
                audioManager.Reset();
                race.Reset();
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
                    {
                        EntityManager.RemoveComponent<Disabled>(smoke.CarSmoke);
                    }
                }).WithStructuralChanges().Run();
            }
        }
    }
}
