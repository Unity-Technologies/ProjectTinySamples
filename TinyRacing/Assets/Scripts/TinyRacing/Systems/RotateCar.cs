using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Rotate cars (no physics movement)
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public class RotateCar : JobComponentSystem
    {
        private EntityQuery RaceQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            RaceQuery = GetEntityQuery(ComponentType.ReadOnly<Race>());
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var raceEntities = RaceQuery.ToEntityArray(Allocator.TempJob);
            var raceEntity = raceEntities[0];
            var race = EntityManager.GetComponentData<Race>(raceEntity);
            var isRaceStarted = race.IsRaceStarted && race.CountdownTimer <= 0f;
            raceEntities.Dispose();
            var deltaTime = Time.DeltaTime;
            
            return Entities.ForEach(
                (ref Car car, ref CarInputs inputs, ref Rotation rotation, ref LapProgress lapProgress) =>
                {
                    var isRaceEnded = lapProgress.CurrentLap > race.LapCount;
                    if (isRaceStarted && !isRaceEnded && !car.IsEngineDestroyed)
                    {
                        if (math.abs(car.CurrentSpeed) > 0.5f)
                        {
                            var rotationSpeed = inputs.HorizontalAxis * car.RotationSpeed * deltaTime * 0.022f;
                            rotation.Value = math.mul(rotation.Value, quaternion.RotateY(rotationSpeed));
                        }
                    }
                }).Schedule(inputDeps);
        }
    }
}