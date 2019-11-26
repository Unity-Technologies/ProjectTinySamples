using Unity.Entities;

namespace TinyRacing
{
    [GenerateAuthoringComponent]
    public struct CarInputs : IComponentData
    {
        public float HorizontalAxis;
        public float AccelerationAxis;
    }
}