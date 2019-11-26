using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     For each car, find the closest control point around the race track to help calculate the progress and current lap.
    /// </summary>
    public class UpdateCarLapProgress : JobComponentSystem
    {
        private EntityQuery ControlPointsQuery;
        public bool AreControlPointsInitialized { get; set; }

        public NativeList<float3> ControlPoints { get; set; }

        protected override void OnCreate()
        {
            base.OnCreate();
            ControlPointsQuery = GetEntityQuery(ComponentType.ReadOnly<ControlPoints>());
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (!AreControlPointsInitialized)
            {
                AreControlPointsInitialized = true;

                var controlPointsEntities = ControlPointsQuery.ToEntityArray(Allocator.TempJob);
                var points = EntityManager.GetBuffer<ControlPoints>(controlPointsEntities[0]).Reinterpret<float3>()
                    .ToNativeArray(Allocator.Temp);
                ControlPoints = new NativeList<float3>(Allocator.Persistent);
                ControlPoints.AddRange(points);
                points.Dispose();
                controlPointsEntities.Dispose();
            }

            var jobHandle = new UpdateCarLapProgressJob
            {
                ControlPoints = ControlPoints
            };

            return jobHandle.Schedule(this, inputDeps);
        }

        public static float3 GetClosestPointOnSegment(float3 subject, float3 pA, float3 pB)
        {
            var AP = subject - pA;
            var AB = pB - pA;

            var magnitudeAB = math.distancesq(pA, pB);
            var ABAPproduct = math.dot(AP, AB);
            var distance = ABAPproduct / magnitudeAB;

            if (distance < 0)
                return pA;
            if (distance > 1)
                return pB;
            return pA + AB * distance;
        }

        protected override void OnDestroy()
        {
            if (AreControlPointsInitialized)
                ControlPoints.Dispose();
        }

        private struct UpdateCarLapProgressJob : IJobForEach<Car, Translation, LapProgress>
        {
            [ReadOnly] public NativeList<float3> ControlPoints;

            public void Execute(ref Car car, ref Translation translation, ref LapProgress lapProgress)
            {
                var carPosition = translation.Value;

                var closestSegmentIndex = -1;
                var closestDistance = float.MaxValue;
                var closestPointOnSegment = float3.zero;

                for (var i = 0; i < ControlPoints.Length; i++)
                {
                    var current = ControlPoints[i];
                    var next = ControlPoints[(i + 1) % ControlPoints.Length];
                    var pointOnSegment = GetClosestPointOnSegment(carPosition, current, next);
                    var distanceToSegment = math.distance(carPosition, pointOnSegment);

                    if (distanceToSegment < closestDistance)
                    {
                        closestSegmentIndex = i;
                        closestDistance = distanceToSegment;
                        closestPointOnSegment = pointOnSegment;
                    }
                }

                var currentPoint = ControlPoints[closestSegmentIndex];
                var nextPoint = ControlPoints[(closestSegmentIndex + 1) % ControlPoints.Length];
                var currentSegmentProgress = math.distance(closestPointOnSegment, currentPoint) /
                                             math.distance(currentPoint, nextPoint);
                var controlPointProgress = closestSegmentIndex + currentSegmentProgress;

                if (controlPointProgress > lapProgress.CurrentControlPoint + lapProgress.CurrentControlPointProgress)
                {
                    lapProgress.CurrentControlPoint = closestSegmentIndex;
                    lapProgress.CurrentControlPointProgress = currentSegmentProgress;
                }
                else if (controlPointProgress < 1f && lapProgress.CurrentControlPoint >= ControlPoints.Length - 1)
                {
                    lapProgress.CurrentControlPoint = closestSegmentIndex;
                    lapProgress.CurrentControlPointProgress = currentSegmentProgress;
                    lapProgress.CurrentLap++;
                }
            }
        }
    }
}