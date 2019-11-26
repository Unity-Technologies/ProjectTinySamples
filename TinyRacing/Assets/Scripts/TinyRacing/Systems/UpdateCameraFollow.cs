using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
#if UNITY_DOTSPLAYER
using Unity.Tiny.Rendering;
using Unity.Tiny.Input;

#endif

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Update camera position and rotation to follow the player.
    /// </summary>
    [UpdateBefore(typeof(TransformSystemGroup))]
    [UpdateAfter(typeof(MoveCar))]
    public class UpdateCameraFollow : ComponentSystem
    {
        private float3 DefaultCameraPosition;
        private quaternion DefaultCameraRot;
        private bool IsDefaultCameraPositionSet;

#if !UNITY_DOTSPLAYER
        UnityEngine.Transform cameraTransform;
#endif
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<Race>();
        }

        protected override void OnStartRunning()
        {
#if !UNITY_DOTSPLAYER
            cameraTransform = UnityEngine.Camera.main.transform;
#endif
        }

        protected override void OnUpdate()
        {
#if UNITY_DOTSPLAYER
            // if shift is held, don't do anything
            if (World.GetExistingSystem<InputSystem>().GetKey(KeyCode.LeftShift))
                return;
#endif
            // Get player car position and direction
            var carPosition = float3.zero;
            var carDirection = float3.zero;
            var carRotation = quaternion.identity;
            Entities.WithNone<AI>().ForEach(
                (ref Car car, ref CarInputs inputs, ref Translation translation, ref LocalToWorld localToWorld,
                    ref Rotation rotation) =>
                {
                    carPosition = translation.Value;
                    carDirection = localToWorld.Forward;
                    carRotation = rotation.Value;
                });
            // Position the camera behind the car
            var race = GetSingleton<Race>();
            var targetPosition = carPosition + new float3(0f, 1.75f, 0f) + carDirection * -5.5f;

            // TODO: Find camera with entity query once there's a pure component for cameras
#if !UNITY_DOTSPLAYER
            var cameraPos = (float3) cameraTransform.position;
            var cameraRot = (quaternion) cameraTransform.rotation;
#else
            var cameraEntity = GetSingletonEntity<Camera>();
            var cameraPos = EntityManager.GetComponentData<Translation>(cameraEntity).Value;
            var cameraRot = EntityManager.GetComponentData<Rotation>(cameraEntity).Value;
#endif

            if (race.IsRaceStarted)
            {
                var deltaTime = math.clamp(Time.DeltaTime * 7f, 0, 1);
                cameraPos = math.lerp(cameraPos, targetPosition, deltaTime);
                cameraRot = math.slerp(cameraRot, carRotation, deltaTime);
            }
            else
            {
                if (!IsDefaultCameraPositionSet)
                {
                    DefaultCameraPosition = cameraPos;
                    DefaultCameraRot = cameraRot;
                    IsDefaultCameraPositionSet = true;
                }

                cameraPos = DefaultCameraPosition;
                cameraRot = DefaultCameraRot;
            }

#if !UNITY_DOTSPLAYER
            cameraTransform.position = cameraPos;
            cameraTransform.rotation = cameraRot;
#else
            EntityManager.SetComponentData(cameraEntity, new Translation {Value = cameraPos});
            EntityManager.SetComponentData(cameraEntity, new Rotation {Value = cameraRot});
#endif

            Entities.ForEach((ref UITag etag, ref Translation pos, ref Rotation rot) =>
            {
                pos.Value = cameraPos;
                rot.Value = cameraRot;
            });
        }
    }
}
