using Unity.Entities;


namespace TinyKitchen
{
    [GenerateAuthoringComponent]
    public struct FoodInstanceComponent : IComponentData
    {
        public Entity Child;
        public bool isLaunched;
        public float timer;  
        public bool playBouncingAudio;
        public bool hasPlayedBouncingAudio;
    }
}
