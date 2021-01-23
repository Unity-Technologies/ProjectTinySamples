using Unity.Entities;

namespace TinyRacing
{
    [GenerateAuthoringComponent]
    public struct UIGameControls : IComponentData
    {
        public Entity ButtonLeft;
        public Entity ButtonRight;
        public Entity ButtonAccelerate;
        public Entity ButtonReverse;
    }
}
