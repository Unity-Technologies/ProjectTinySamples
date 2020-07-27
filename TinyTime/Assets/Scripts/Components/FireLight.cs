using Unity.Entities;

namespace TinyTime
{

    [GenerateAuthoringComponent]
    public struct FireLight : IComponentData
    {
        public float Intensity;
    }
}
