using Unity.Entities;

namespace TinyPhysics
{
    public enum ElevatorState
    {
        Stopped,
        GoingUp,
        GoingDown
    }

    [GenerateAuthoringComponent]
    public struct Elevator : IComponentData
    {
        public float loweredPosition;
        public float raisedPosition;
        public float moveSpeed;

        public ElevatorState ElevatorState { get; set; }
    }
}
