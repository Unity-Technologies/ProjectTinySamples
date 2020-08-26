using Unity.Entities;
using Unity.Mathematics;

using Unity.Tiny;
using Unity.Tiny.Rendering;

namespace Unity.TinyGems
{
    public static class CameraUtil
    {
        private const float k_NearClip = 0.3f;

        public static float2 ScreenPointToWorldPoint(World world, float2 screenPoint)
        {
#if UNITY_DOTSRUNTIME
            var screenToWorldSystem = world.GetExistingSystem<ScreenToWorld>();
            var worldPoint = screenToWorldSystem.ScreenSpaceToWorldSpacePos(screenPoint, k_NearClip, ScreenToWorldId.Sprites);
            return worldPoint.xy;
#else
            if (UnityEngine.Camera.main == null)
                throw new System.Exception("No camera available. Make sure the Main Camera sub scene is in edit mode.");

            var screenPointVec = new UnityEngine.Vector3(screenPoint.x, screenPoint.y, 0f);
            var worldPointVec = UnityEngine.Camera.main.ScreenToWorldPoint(screenPointVec);
            return new float2(worldPointVec.x, worldPointVec.y);
#endif
        }
    }
}