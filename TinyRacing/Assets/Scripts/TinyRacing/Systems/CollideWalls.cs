using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Simulate wall collisions by clamping the car position near the center of
    ///     the race track defined by control points.
    /// </summary>
    [UpdateAfter(typeof(MoveCar))]
    public class CollideWalls : JobComponentSystem
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<Race>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var race = GetSingleton<Race>();
            if (!race.IsRaceStarted)
                return inputDeps;

            var updateCarLapSystem = World.GetExistingSystem<UpdateCarLapProgress>();
            if (!updateCarLapSystem.AreControlPointsInitialized)
                return inputDeps;
            
            var controlPoints = updateCarLapSystem.ControlPoints;
            return Entities.ForEach((ref LapProgress lapProgress, ref Translation translation) =>
            {
                var controlPointIndex = lapProgress.CurrentControlPoint;
                var controlPointProgress = lapProgress.CurrentControlPointProgress;
                var firstControlPointIndex = controlPointIndex == 0 ? controlPoints.Length - 1 : controlPointIndex - 1;
                var closestPoint = SplineUtils.GetPoint(controlPoints, firstControlPointIndex, controlPointProgress);
                var distanceFromCenterOfTrackSq = math.distancesq(closestPoint, translation.Value);

                var maxDistanceFromCenterOfRoad = 6.4f;
                if (distanceFromCenterOfTrackSq > maxDistanceFromCenterOfRoad * maxDistanceFromCenterOfRoad)
                {
                    var clampedPosition = closestPoint +
                                          math.normalize(translation.Value - closestPoint) *
                                          maxDistanceFromCenterOfRoad;
                    translation.Value.xz = clampedPosition.xz;
                }
            }).WithReadOnly(controlPoints).Schedule(inputDeps);
        }
    }
}