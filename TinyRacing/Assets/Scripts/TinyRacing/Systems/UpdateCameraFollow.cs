using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Input;
using Unity.Transforms;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Update camera position and rotation to follow the player.
    /// </summary>
    [UpdateBefore(typeof(TransformSystemGroup))]
    [UpdateAfter(typeof(MoveCar))]
    public class UpdateCameraFollow : SystemBase
    {
        private float3 DefaultCameraPosition;
        private quaternion DefaultCameraRot;
        private bool IsDefaultCameraPositionSet;

        protected override void OnUpdate()
        {
            RequireSingletonForUpdate<MainCamera>();

            // if shift is held, don't do anything
            if (World.GetExistingSystem<InputSystem>().GetKey(KeyCode.LeftShift))
            {
                return;
            }

            // Get player car position and direction
            var carPosition = float3.zero;
            var carDirection = float3.zero;
            var carRotation = quaternion.identity;
            Entities.WithAll<Player>().ForEach(
                (ref Car car, ref CarInputs inputs, in Translation translation, in LocalToWorld localToWorld,
                    in Rotation rotation) =>
                {
                    carPosition = translation.Value;
                    carDirection = localToWorld.Forward;
                    carRotation = rotation.Value;
                }).Run();
            // Position the camera behind the car
            if (!HasSingleton<Race>())
            {
                return;
            }

            var race = GetSingleton<Race>();
            var targetPosition = carPosition + new float3(0f, 1.75f, 0f) + carDirection * -5.5f;

            // TODO: Find camera with entity query once there's a pure component for cameras

            var cameraEntity = GetSingletonEntity<MainCamera>();
            var cameraPos = EntityManager.GetComponentData<Translation>(cameraEntity).Value;
            var cameraRot = EntityManager.GetComponentData<Rotation>(cameraEntity).Value;

            var deltaTime = math.clamp(Time.DeltaTime * 7f, 0, 1);
            if (race.IsInProgress())
            {
                cameraPos = math.lerp(cameraPos, targetPosition, deltaTime);
                cameraRot = math.slerp(cameraRot, carRotation, deltaTime);
            }
            else if (race.IsFinished() && HasSingleton<EndingCameraPostition>())
            {
                var endingCameraPositionEntity = GetSingletonEntity<EndingCameraPostition>();
                var endingCameraPos = EntityManager.GetComponentData<Translation>(endingCameraPositionEntity).Value;
                var endingCameraRot = EntityManager.GetComponentData<Rotation>(endingCameraPositionEntity).Value;

                cameraPos = math.lerp(cameraPos, endingCameraPos, deltaTime);
                cameraRot = math.slerp(cameraRot, endingCameraRot, deltaTime);
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

            EntityManager.SetComponentData(cameraEntity, new Translation {Value = cameraPos});
            EntityManager.SetComponentData(cameraEntity, new Rotation {Value = cameraRot});
        }
    }
}
