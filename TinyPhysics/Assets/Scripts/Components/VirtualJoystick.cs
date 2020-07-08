using Unity.Entities;
using Unity.Mathematics;

namespace TinyPhysics
{
    [GenerateAuthoringComponent]
    public struct VirtualJoystick : IComponentData
    {
        public Entity knob;
        public float inputRadius;

        public bool IsPressed { get; set; }
        public int PointerId { get; set; }
        public float2 Center { get; set; }
        public float2 Value { get; set; }
    }
}
