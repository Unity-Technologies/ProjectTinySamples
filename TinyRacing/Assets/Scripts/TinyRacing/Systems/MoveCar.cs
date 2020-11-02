using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
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
            Entities.ForEach((ref Car car, ref SpeedMultiplier speedMultiplier, ref Rotation rotation, ref PhysicsMass mass,
                ref CarInputs inputs, ref LocalToWorld localToWorld,  ref PhysicsVelocity velocity, in LapProgress lapProgress) =>
                {
                    if (race.IsRaceFinished)
                    {
                        car.CurrentSpeed = 0;
                        return;
                    }
                    if (isRaceStarted && !car.IsEngineDestroyed)
                    {
                        speedMultiplier.RemainingTime -= deltaTime;

                        var maxSpeed = car.MaxSpeed;
                        var targetSpeed = maxSpeed * inputs.AccelerationAxis;
                        car.CurrentSpeed = math.lerp(car.CurrentSpeed, targetSpeed, deltaTime);

                        var hasSpeedBoost = speedMultiplier.RemainingTime > 0f;
                        if (hasSpeedBoost)
                            car.CurrentSpeed = maxSpeed * speedMultiplier.Multiplier;

                        var currentVelocity = localToWorld.Forward * car.CurrentSpeed;
                        velocity.Linear = new float3(currentVelocity.x, velocity.Linear.y, currentVelocity.z);
                        var rotationSpeed = 0f;
                        if (math.abs(car.CurrentSpeed) > 0.2f)
                        {
                            rotationSpeed = inputs.HorizontalAxis * car.RotationSpeed;
                        }
                        var angular = new float3(0f, rotationSpeed, 0f);
                        //Change the steering direction on reverse
                        if (inputs.AccelerationAxis < 0)
                            angular = -angular;

                        var q = rotation.Value;
                        var angle = 2.0f * math.atan(q.value.y / q.value.w);
                        rotation.Value = quaternion.RotateY(angle);

                        velocity.SetAngularVelocityWorldSpace(mass, rotation , angular);
                    }
                }).ScheduleParallel();
        }
    }
}
