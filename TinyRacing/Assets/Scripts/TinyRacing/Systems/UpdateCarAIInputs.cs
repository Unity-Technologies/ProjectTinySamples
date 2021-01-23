using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Move the AI controlled cars along the control points of the track by simulating controller inputs
    /// </summary>
    [UpdateAfter(typeof(TransformSystemGroup))]
    public class UpdateCarAIInputs : SystemBase
    {
        protected override void OnUpdate()
        {
            var race = GetSingleton<Race>();
            if (!race.IsInProgress())
            {
                return;
            }

            var updateCarLapSystem = EntityManager.World.GetExistingSystem<UpdateCarLapProgress>();
            var controlPoints = updateCarLapSystem.ControlPoints;

            Dependency = Entities.WithAll<Car>().ForEach((ref Car car, ref CarInputs inputs, in AI opponent,
                in LocalToWorld localToWorld, in LapProgress lapProgress,
                in Translation translation) =>
            {
                var controlPointIndex = lapProgress.CurrentControlPoint;
                var controlPointProgress = lapProgress.CurrentControlPointProgress;
                var firstControlPointIndex = controlPointIndex == 0 ? controlPoints.Length - 1 : controlPointIndex - 1;
                var closestPoint = SplineUtils.GetPoint(controlPoints, firstControlPointIndex, controlPointProgress);
                var tangent = SplineUtils.GetTangent(controlPoints, firstControlPointIndex, controlPointProgress);
                var targetPoint = closestPoint + tangent * 4f +
                                  math.cross(tangent, new float3(0f, 1f, 0f)) * opponent.NormalDistanceFromTrack;

                var current = new float2(translation.Value.x, translation.Value.z);
                var target = new float2(targetPoint.x, targetPoint.z);
                var wantedDirection = target - current;
                var currentDirection = new float2(localToWorld.Forward.x, localToWorld.Forward.z);

                var angleCurrentDirection = math.atan2(currentDirection.y, currentDirection.x);
                angleCurrentDirection = math.degrees(angleCurrentDirection);
                var angleWantedDirection = math.atan2(wantedDirection.y, wantedDirection.x);
                angleWantedDirection = math.degrees(angleWantedDirection);
                var angleDiff = angleWantedDirection - angleCurrentDirection;

                // Steer the car to follow the curve of the track
                if (angleDiff < 180f && angleDiff > -180f)
                {
                    inputs.HorizontalAxis = angleDiff > 0f ? -1f : 1f;
                }
                else
                {
                    inputs.HorizontalAxis = angleDiff > 0f ? 1f : -1f;
                }

                // AI cars always accelerate to full speed
                inputs.AccelerationAxis = 1f;
            }).WithReadOnly(controlPoints).ScheduleParallel(Dependency);
        }
    }
}
