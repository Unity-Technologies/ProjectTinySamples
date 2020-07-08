using Unity.Entities;

namespace TinyPhysics
{
    [GenerateAuthoringComponent]
    public struct MoveWithVirtualJoystick : IComponentData
    {
        public Entity movementJoystick;
        public Entity jumpButton;
    }
}
