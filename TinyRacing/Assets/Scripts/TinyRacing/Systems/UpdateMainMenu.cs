using System;
using Unity.Entities;
using Unity.Transforms;
using Unity.Scenes;
#if UNITY_DOTSRUNTIME
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
    [UpdateAfter(typeof(TransformSystemGroup))]
    public class UpdateMainMenu : SystemBase
    {
        protected override void OnUpdate()
        {
            // Hide main menu when user presses any key
#if !UNITY_DOTSRUNTIME
            bool startRaceButtonPressed = Input.anyKeyDown;
#else
            var Input = World.GetExistingSystem<InputSystem>();
            var startRaceButtonPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) || Input.TouchCount() > 0;
#endif
            if (!HasSingleton<Race>())
            {
                var sceneSystem = World.GetExistingSystem<SceneSystem>();
                var raceSceneEntity = GetSingletonEntity<RaceScene>();
                var raceScene = EntityManager.GetComponentData<SceneReference>(raceSceneEntity);
                sceneSystem.LoadSceneAsync(raceScene.SceneGUID, new SceneSystem.LoadParameters() { AutoLoad = true, Flags = SceneLoadFlags.LoadAdditive });

                return;
            }

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
#if UNITY_DOTSRUNTIME
                Entities
                    .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled)
                    .WithAll<MainMenuTag, AudioSource, Disabled>()
                    .ForEach((Entity entity) =>
                {
                    EntityManager.AddComponent<AudioSourceStart>(entity);
                }).WithStructuralChanges().Run();
#endif
                Entities
                    .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled)
                    .WithAll<MainMenuTag, Disabled>().ForEach((Entity entity) =>
                    {
                    EntityManager.RemoveComponent<Disabled>(entity);
                }).WithStructuralChanges().Run();
            }
            else
            {
                Entities
                    .WithAll<MainMenuTag>()
                    .ForEach((Entity entity) =>
                    {
                        EntityManager.AddComponent<Disabled>(entity);
                    })
                    .WithStructuralChanges().Run();
            }
        }
    }
}
