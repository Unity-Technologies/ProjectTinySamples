using Unity.Entities;

namespace TinyPhysics
{
    public enum SwipeDirection
    {
        None,
        Up,
        Down,
        Left,
        Right
    }

    [GenerateAuthoringComponent]
    public struct Swipeable : IComponentData
    {
        public SwipeDirection SwipeDirection { get; set; }
    }
}
