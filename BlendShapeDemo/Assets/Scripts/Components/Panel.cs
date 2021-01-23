using Unity.Entities;

namespace BlendShapeDemo
{
    [GenerateAuthoringComponent]
    public struct Panel : IComponentData
    {
        public GameState gameState;
    }
}

