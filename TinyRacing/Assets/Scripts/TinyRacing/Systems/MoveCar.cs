using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Move cars without physics.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public class MoveCar : SystemBase
    {
        protected override void OnUpdate()
        {
            var race = GetSingleton<Race>();
            var isRaceStarted = race.IsRaceStarted && race.CountdownTimer <= 0f;
            var deltaTime = Time.DeltaTime;
            var totalLapCount = race.LapCount;
            Entities.ForEach((ref Car car, ref SpeedMultiplier speedMultiplier, ref Rotation rotation, ref Translation translation,
                ref CarInputs inputs, ref LocalToWorld localToWorld,  ref PhysicsVelocity velocity, in LapProgress lapProgress) =>
            {
                var isRaceEnded = lapProgress.CurrentLap > totalLapCount;
                if (isRaceStarted && !isRaceEnded && !car.IsEngineDestroyed)
                {
                    speedMultiplier.RemainingTime -= deltaTime;

                    var maxSpeed = car.MaxSpeed;
                    var targetSpeed = maxSpeed * inputs.AccelerationAxis;
                    car.CurrentSpeed = math.lerp(car.CurrentSpeed, targetSpeed, deltaTime);

                    var hasSpeedBoost = speedMultiplier.RemainingTime > 0f;
                    if (hasSpeedBoost)
                        car.CurrentSpeed = maxSpeed * speedMultiplier.Multiplier;

                    var currentVelocity = localToWorld.Forward * car.CurrentSpeed * deltaTime;
                    velocity.Linear = new float3(currentVelocity.x, velocity.Linear.y, currentVelocity.z);
                    var rotationSpeed = 0f;
                    if (math.abs(car.CurrentSpeed) > 0.2f)
                    {
                        rotationSpeed = inputs.HorizontalAxis * car.RotationSpeed * deltaTime;
                    }
                    velocity.Angular = new float3(0f, rotationSpeed, 0f);

                    // TODO -- make these constraints better, don't hardcode them

                    // Make the car rotate only in Y axis
                    var q = rotation.Value;
                    var angle = 2.0f * math.atan(q.value.y / q.value.w);
                    rotation.Value = quaternion.RotateY(angle);

                    // Don't let the car sink below track level
                    if (translation.Value.y < 0.58)
                        translation.Value.y = 0.58f;
                }
                else if (isRaceEnded)
                {
                    car.CurrentSpeed = 0;
                }
            }).ScheduleParallel();
        }
        
    }
}
