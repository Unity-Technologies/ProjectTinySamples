using Unity.Entities;

namespace TinyKitchen
{
    [GenerateAuthoringComponent]
    public struct AudioManager : IComponentData
    {
        public Entity FlyingFoodAudio;
        public Entity BouncingFoodAudio;
        public Entity DestroyFoodAudio;
        public Entity TouchPotAudio;
        public Entity SuccesAudio;
    }
}

