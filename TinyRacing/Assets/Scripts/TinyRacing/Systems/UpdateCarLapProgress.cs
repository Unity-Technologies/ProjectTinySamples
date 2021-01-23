using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     For each car, find the closest control point around the race track to help calculate the progress and current lap.
    /// </summary>
    public class UpdateCarLapProgress : SystemBase
    {
        private EntityQuery ControlPointsQuery;
        public NativeArray<float3> ControlPoints { get; set; }

        protected override void OnCreate()
        {
            base.OnCreate();
            ControlPointsQuery = GetEntityQuery(ComponentType.ReadOnly<ControlPoints>());
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            var controlPointsEntities = ControlPointsQuery.ToEntityArray(Allocator.TempJob);
            ControlPoints = EntityManager.GetBuffer<ControlPoints>(controlPointsEntities[0]).Reinterpret<float3>()
                .ToNativeArray(Allocator.Persistent);
            controlPointsEntities.Dispose();
        }

        protected override void OnUpdate()
        {
            var race = GetSingleton<Race>();
            if (!race.IsInProgress())
            {
                return;
            }

            var controlPoints = ControlPoints;
            var time = Time;

            // Schedule a parallel job to compute each car's position around the track
            Dependency = Entities.ForEach(
                (ref Car car, ref CarRank rank, ref LapProgress lapProgress, in Translation translation) =>
                {
                    var carPosition = translation.Value;

                    // Find where the car is closest to the track
                    ComputeClosest(controlPoints, carPosition, out var closestDistance, out var closestSegmentIndex,
                        out var closestPointOnSegment);

                    var currentPoint = controlPoints[closestSegmentIndex];
                    var nextPoint = controlPoints[(closestSegmentIndex + 1) % controlPoints.Length];
                    var currentSegmentProgress = math.distance(closestPointOnSegment, currentPoint) /
                                                 math.distance(currentPoint, nextPoint);
                    var controlPointProgress = closestSegmentIndex + currentSegmentProgress;

                    // is this the beginning of the race and we just crossed the line?
                    if (lapProgress.CurrentControlPoint == -1 && controlPointProgress < 0.0f)
                    {
                        return;
                    }

                    // did we just complete a lap?
                    if (controlPointProgress < 1f && lapProgress.CurrentControlPoint >= controlPoints.Length - 1)
                    {
                        lapProgress.CurrentLap++;
                        rank.LastLapTime = race.RaceTimer; //(float) (time.ElapsedTime - race.RaceStartTime);
                    }

                    lapProgress.CurrentControlPoint = closestSegmentIndex;
                    lapProgress.CurrentControlPointProgress = currentSegmentProgress;
                    lapProgress.TotalProgress = lapProgress.CurrentLap * 1000f + controlPointProgress;
                }).WithReadOnly(controlPoints).ScheduleParallel(Dependency);
        }

        private static void ComputeClosest(NativeArray<float3> controlPoints,
            float3 carPosition,
            out float closestDistance, out int closestSegmentIndex, out float3 closestPointOnSegment)
        {
            closestDistance = float.MaxValue;
            closestSegmentIndex = -1;
            closestPointOnSegment = float3.zero;

            for (var i = 0; i < controlPoints.Length; i++)
            {
                var current = controlPoints[i];
                var next = controlPoints[(i + 1) % controlPoints.Length];
                var pointOnSegment = GetClosestPointOnSegment(carPosition, current, next);
                var distanceToSegment = math.distance(carPosition, pointOnSegment);

                if (distanceToSegment < closestDistance)
                {
                    closestSegmentIndex = i;
                    closestDistance = distanceToSegment;
                    closestPointOnSegment = pointOnSegment;
                }
            }
        }

        public static float3 GetClosestPointOnSegment(float3 subject, float3 pA, float3 pB)
        {
            var AP = subject - pA;
            var AB = pB - pA;

            var magnitudeAB = math.distancesq(pA, pB);
            var ABAPproduct = math.dot(AP, AB);
            var distance = ABAPproduct / magnitudeAB;

            if (distance < 0)
            {
                return pA;
            }

            if (distance > 1)
            {
                return pB;
            }

            return pA + AB * distance;
        }

        protected override void OnDestroy()
        {
            ControlPoints.Dispose();
        }
    }
}
