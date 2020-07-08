using Unity.Entities;

namespace TinyPhysics
{
    [GenerateAuthoringComponent]
    public struct Jumper : IComponentData
    {
        public float jumpImpulse;

        public bool JumpTrigger { get; set; }
    }
}
