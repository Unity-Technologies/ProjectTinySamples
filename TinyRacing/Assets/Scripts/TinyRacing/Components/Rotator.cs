using Unity.Entities;

namespace TinyRacing
{
    [GenerateAuthoringComponent]
    public struct Rotator : IComponentData
    {
        public float RotateSpeed;
    }
}