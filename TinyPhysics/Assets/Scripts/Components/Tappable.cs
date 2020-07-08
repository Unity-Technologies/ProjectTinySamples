using Unity.Entities;

namespace TinyPhysics
{
    [GenerateAuthoringComponent]
    public struct Tappable : IComponentData
    {
        public int PointerId { get; set; }
        public double TimePressed { get; set; }
        public float PointerMoveSqrDistance { get; set; }

        public bool IsPressed { get; set; }
        public bool IsTapped { get; set; }
    }
}
