using Unity.Entities;

namespace TinyRacing
{
    [GenerateAuthoringComponent]
    public struct Smoke : IComponentData
    {
        public float Timer;
        public float Duration;
        public float BaseScale;
        public float Speed;
    }
}