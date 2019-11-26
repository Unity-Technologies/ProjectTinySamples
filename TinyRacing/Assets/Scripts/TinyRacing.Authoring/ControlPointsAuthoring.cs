using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TinyRacing.Authoring
{

[DisallowMultipleComponent]
public class ControlPointsAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddBuffer<ControlPoints>(entity);

        var pointBuffer = dstManager.GetBuffer<ControlPoints>(entity);
        var pointCount = transform.childCount;
        for (int i = 0; i < pointCount; i++)
            pointBuffer.Add(new ControlPoints() { Position = transform.GetChild(i).position });
    }

    // Draw the path between control points
    void OnDrawGizmos()
    {
        var pointCount = transform.childCount;
        for (int i = 0; i < pointCount; i++)
        {

            var previousPosition = (i - 1 < 0) ? transform.GetChild(pointCount - 1).position : transform.GetChild(i - 1).position;
            var startPosition = transform.GetChild(i).position;
            var endPosition = transform.GetChild((i + 1) % pointCount).position;
            var nextPosition = transform.GetChild((i + 2) % pointCount).position;

            var controlPoints = new float3[4] { previousPosition, startPosition, endPosition, nextPosition };

            Gizmos.color = Color.green;
            DrawCurve(controlPoints);
            //DrawNormals(splineSegment);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(startPosition, 0.4f);
        }
    }

    void DrawCurve(float3[] controlPoints)
    {
        var lineCount = 50;
        for (int i = 1; i <= lineCount; i++)
        {
            var previousRatio = (i - 1) / (float)lineCount;
            var ratio = i / (float)lineCount;
            using (NativeArray<float3> points = new NativeArray<float3>(controlPoints, Allocator.Temp))
            {
                var previousPosition = SplineUtils.GetPoint(points, 0, previousRatio);
                var currentPosition = SplineUtils.GetPoint(points, 0, ratio);
                Gizmos.DrawLine(previousPosition, currentPosition);
            }
        }
    }
}

}
