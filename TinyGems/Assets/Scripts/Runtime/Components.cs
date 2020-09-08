using Unity.Entities;
using Unity.Mathematics;

namespace Unity.TinyGems
{
    public struct CellManager : IComponentData
    {
        public Entity CellPrefab;
        public Entity CellBackground;
        public int MaxCol;
        public int MaxRow;
    }

    public struct CellSprite : IBufferElementData
    {
        public Entity Sprite;
    }

    public struct HeartManager : IComponentData
    {
        public Entity HeartPrefab;
        public Entity HeartBackgroundPrefab;
        public int NoOfHearts;
    }
    
    public struct Heart : IBufferElementData
    {
        public Entity Value;
    }    
    
    public struct HeartBackground : IComponentData
    {
    }

    public struct CellBackground : IComponentData
    {
    }

    public struct Cell : IComponentData
    {
        public int2 Position;
        public int Color;
    }

    public struct Swap : IComponentData
    {
        public float Progress;
        public int2 Position;
    }

    public enum Scenes
    {
        None,
        Title,
        Game
    }

    public struct ActiveScene : IComponentData
    {
        public Scenes Value;
    }
    
    public enum GameState
    {
        None,
        Spawn,
        Drop,
        Move,
        Match,
        Swap,
        GameOver
    }
    
    public struct GameStateData : IComponentData
    {
        public GameState State;
        public int Hearts;
    }
    
    public struct ActiveInput : IComponentData
    {
        public SwapInput Value;
    }

    public struct SwapInput
    {
        public Entity Cell;
        public float2 DeltaInput;
    }
    
    public struct HudShowState : IComponentData
    {
        public Scenes Value;
    }

    public struct HudObject : IBufferElementData
    {
        public Entity Value;
    }

    public struct AudioLibrary : IComponentData
    {
    }

    public enum AudioTypes
    {
        None,
        StartGame,
        Match0,
        Match1,
        Match2,
        Match3,
        Fail0,
        Fail1,
        Fail2,
        Fail3,
        GameOver
    }
    
    public struct AudioObject : IBufferElementData
    {
        public AudioTypes Type;
        public Entity Clip;
    }    
}