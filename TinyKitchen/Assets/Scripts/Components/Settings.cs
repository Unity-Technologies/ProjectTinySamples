using Unity.Entities;
using Unity.Mathematics;

namespace TinyKitchen
{
    [GenerateAuthoringComponent]
    public struct SettingsSingleton : IComponentData
    {
        public float2 inputScale;
        public float sensitivity; // (0, 1)
        public float launchHeight;
        public float launchStrengthMultiplier;
        public float maxAirtime;
        public float launchRotateAmount;
        public float transparentTime;
        public float changeLevelTime;
        public float potEdgeThickness;
        public float maxInputAngle;
        public float3 cameraFollowAmount;
        public float cameraTrackAmount;
        public float cameraDamp;
        public float launchExp;
        public float heightExp;
        public float effectTime;
    }
}
