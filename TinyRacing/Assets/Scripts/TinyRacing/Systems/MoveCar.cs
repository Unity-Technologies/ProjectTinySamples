using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Move cars without physics.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public class MoveCar : JobComponentSystem
    {
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var race = GetSingleton<Race>();
            var isRaceStarted = race.IsRaceStarted && race.CountdownTimer <= 0f;
            var deltaTime = Time.DeltaTime;
            var totalLapCount = race.LapCount;
            
            return Entities.ForEach((ref Car car, ref SpeedMultiplier speedMultiplier, ref Translation translation,
                in CarInputs inputs, in LocalToWorld localToWorld, in LapProgress lapProgress) =>
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

                    var currentVelocity = localToWorld.Forward * car.CurrentSpeed * deltaTime * 0.025f;
                    translation.Value.xz += currentVelocity.xz;
                }
                else if (isRaceEnded)
                {
                    car.CurrentSpeed = 0;
                }
            }).Schedule(inputDeps);
        }
    }
}