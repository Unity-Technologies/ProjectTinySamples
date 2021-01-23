using Unity.Entities;
using Unity.Scenes;
using Unity.Tiny.UI;
using Unity.Transforms;
using UnityEngine;

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
            if (!HasSingleton<Race>())
            {
                var sceneSystem = World.GetExistingSystem<SceneSystem>();
                var raceSceneEntity = GetSingletonEntity<RaceScene>();
                var raceScene = EntityManager.GetComponentData<SceneReference>(raceSceneEntity);
                sceneSystem.LoadSceneAsync(raceScene.SceneGUID,
                    new SceneSystem.LoadParameters {AutoLoad = true, Flags = SceneLoadFlags.LoadAdditive});

                return;
            }

            var race = GetSingleton<Race>();

            // Find the start button
            var StartButtonEntity = World.GetExistingSystem<ProcessUIEvents>().GetEntityByUIName("StartButton");

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
                if (StartButtonEntity == eClicked)
                {
                    Debug.Log("Starting race");
                    race.Start();
                    race.CountdownTimer = race.CountdownTime;
                    race.RaceTimer = 0f;
                    SetSingleton(race);
                    StoreDefaultState();
                }
            }
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
    }
}
