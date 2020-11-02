using Unity.Entities;

namespace TinyKitchen
{
    [GenerateAuthoringComponent]
    public struct FoodComponent : IBufferElementData
    {
        public Entity Food;
    }
}
