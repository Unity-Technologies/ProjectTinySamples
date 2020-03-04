using Unity.Tiny;
using Unity.Tiny.Rendering;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Spaceship
{
    public static class CameraUtil
    {
        private const float k_TargetAspectRatio = 1920f / 1080f;
        private const float k_NearClip = 0.3f;
        
        public static float2 ScreenPointToViewportPoint(EntityManager entityManager, float2 screenPoint)
        {
#if UNITY_DOTSPLAYER
            var displayInfo = entityManager.CreateEntityQuery(typeof(DisplayInfo)).GetSingleton<DisplayInfo>();
            var screenSize = GetScreenSize(displayInfo);
            
            var aspectRatio = (float)displayInfo.width / (float)displayInfo.height;
            if (aspectRatio > k_TargetAspectRatio)
            {
                screenPoint.x -= (displayInfo.width - screenSize.x) / 2f;
            }
            else
            {
                screenPoint.y -= (displayInfo.height - screenSize.y) / 2f;
            }
#else
            var screenSize = GetScreenSize();
#endif
            screenPoint.x = (float)((screenPoint.x / (double)screenSize.x * 2d) - 1d); 
            screenPoint.y = (float)((screenPoint.y / (double)screenSize.y * 2d) - 1d);
            
            return screenPoint;
        }
        
        private static int2 GetScreenSize(DisplayInfo displayInfo)
        {
            var width = displayInfo.width;
            var height = displayInfo.height;
            
            var aspectRatio = (float)width / (float)height;
            if (aspectRatio > k_TargetAspectRatio)
            {
                width = (int)(height * k_TargetAspectRatio);
                return new int2(width, height);
            }
            else
            {
                height = (int)(width / k_TargetAspectRatio);
                return new int2(width, height);
            }
        }        
        
#if UNITY_EDITOR        
        private static int2 GetScreenSize()
        {
            return new int2(UnityEngine.Screen.width, UnityEngine.Screen.height);
        }      
#endif
        
        public static float2 ViewPortPointToNearClipPoint(CameraMatrices cameraMatrices, float2 viewportPoint)
        {
            var modelViewMat = math.mul(cameraMatrices.view, float4x4.identity);
            var mvpMat = math.mul(cameraMatrices.projection, modelViewMat);
            var inverseMvpMat = math.inverse(mvpMat);

            var position = new float4(viewportPoint.x, viewportPoint.y, k_NearClip, 1f);
            position = math.mul(inverseMvpMat, position);

            if (position.w == 0f)
                return float2.zero;

            position.w = 1f / position.w;
            position.x *= position.w;
            position.y *= position.w;
            position.z *= position.w;

            return position.xy;
        }
    }
}