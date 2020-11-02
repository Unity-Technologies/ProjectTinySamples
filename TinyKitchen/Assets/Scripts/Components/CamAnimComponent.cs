using Unity.Entities;
using Unity.Mathematics;

namespace TinyKitchen
{
    [GenerateAuthoringComponent]
    public struct CamAnimComponent : IComponentData
    {
        public float3 origin;
        public quaternion id;
        public float3 lastPos;
        public quaternion lastRot;
    }
}
