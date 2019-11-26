using Unity.Entities;

namespace TinyRacing
{
    [GenerateAuthoringComponent]
    public struct LabelNumber : IComponentData
    {
        public bool IsVisible;
        public int Number;
    }
}