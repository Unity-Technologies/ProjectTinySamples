using Unity.Entities;

namespace BlendShapeDemo
{
    [GenerateAuthoringComponent]
    public struct Cursor : IComponentData
    {
        public float minRange;
        public float maxRange;
        public int blendShapeOrder;
    }
}
