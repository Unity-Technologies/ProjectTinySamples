using Unity.Entities;

namespace TinyPhysics
{
    [GenerateAuthoringComponent]
    public struct Draggable : IComponentData
    {
        public int PointerId { get; set; }
        public bool IsDragged { get; set; }
    }
}
