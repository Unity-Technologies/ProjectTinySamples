using Unity.Entities;
using Unity.Mathematics;

namespace TinyTime
{
    public enum LoopType {None, Loop, PingPong}
    [GenerateAuthoringComponent]
    public struct Move:IComponentData
    {
        public float Speed;
        public LoopType Loop;
        public float3 Destination;
        public float3 InitialPosition;
    }
}
