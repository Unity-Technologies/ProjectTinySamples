using Unity.Entities;

namespace TinyKitchen
{
    public enum GameState
    {
        Initialization = 0, 
        Idle,
        Aiming,
        Flying,
        Landed,
        Missed,
        Scored
    }

    [GenerateAuthoringComponent]
    public struct Game : IComponentData
    {
        public GameState gameState;
        public Entity FoodOnSpatula;
        public int currentLevel;
        public int score;
        public bool scoredLast;     
    }  
}
