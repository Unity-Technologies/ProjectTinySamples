using Unity.Collections;
using Unity.Mathematics;

/// <summary>
///     Calculate point, tangent, etc on a spline defined by control points.
/// </summary>
public class SplineUtils
{
    // https://www.habrador.com/tutorials/interpolation/1-catmull-rom-splines/
    // http://www.iquilezles.org/www/articles/minispline/minispline.htm
    public static float3 GetPoint(NativeArray<float3> points, int firstPoint, float t)
    {
        var p0 = points[firstPoint];
        var p1 = points[(firstPoint + 1) % points.Length];
        var p2 = points[(firstPoint + 2) % points.Length];
        var p3 = points[(firstPoint + 3) % points.Length];

        float3 a = 2f * p1;
        float3 b = p2 - p0;
        float3 c = 2f * p0 - 5f * p1 + 4f * p2 - p3;
        float3 d = -p0 + 3f * p1 - 3f * p2 + p3;

        return 0.5f * (a + (b * t) + (c * t * t) + (d * t * t * t));
    }

    public static float3 GetTangent(NativeArray<float3> points, int firstPoint, float t)
    {
        var p0 = GetPoint(points, firstPoint, t == 1f ? 0.99999f : t);
        var p1 = GetPoint(points, firstPoint, t == 1f ? 1f : t + 0.00001f);
        return math.normalize(p1 - p0);
    }
}