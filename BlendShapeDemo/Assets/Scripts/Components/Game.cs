using Unity.Entities;

namespace BlendShapeDemo 
{
    public enum GameState 
    {
        Idle,
        Moving,
        Billy,
        Emily   
    }

    [GenerateAuthoringComponent]
    public struct Game : IComponentData
    {
        public GameState gameState;
    }
}
