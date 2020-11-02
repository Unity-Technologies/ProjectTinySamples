using Unity.Entities;

namespace TinyKitchen
{
    [GenerateAuthoringComponent]
    public struct Flipper : IComponentData
    {
        public Entity FlipperAudio;
    }
   
}
