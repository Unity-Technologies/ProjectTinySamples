using Unity.Entities;

namespace Tiny2D
{
    [GenerateAuthoringComponent]
    public struct RotationSpeed : IComponentData
    {
        public float Value;
    }
}
