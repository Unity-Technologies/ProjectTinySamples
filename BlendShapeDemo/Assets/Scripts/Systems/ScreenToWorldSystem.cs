using Unity.Entities;
using Unity.Tiny.Rendering;
using Unity.Mathematics;

namespace BlendShapeDemo 
{
    /// <summary>
    /// Convert screen point to world point
    /// </summary>

    public static class ScreenToWorldSystem
    {
        // Camera distance
        private const float nearClip = 6.5f;

        public static float3 ScreenPointToWorldPoint(World world, float screenPoint)
        {
            var screenToWorldSystem = world.GetExistingSystem<ScreenToWorld>();
            var worldPoint = screenToWorldSystem.ScreenSpaceToWorldSpacePos(screenPoint, nearClip);
            return worldPoint;
        }
    }
}
