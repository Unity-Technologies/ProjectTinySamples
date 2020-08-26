using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Spaceship
{
    public struct Asteroid : IComponentData
    {
    }

    public struct AsteroidSpawner : IComponentData
    {
        public Entity Prefab;
        public float Rate;
        public float MinSpeed;
        public float MaxSpeed;
        public float PathVariation;
    }

    public struct AsteroidSprite : IBufferElementData
    {
        public Entity Sprite;
    }

    public struct ExplosionSpawner : IComponentData
    {
        public Entity Prefab;
        public float TimePerSprite;
    }

    public struct ExplosionSprite : IBufferElementData
    {
        public Entity Sprite;
    }

    public struct ActiveInput : IComponentData
    {
        public bool Reverse;
        public bool Accelerate;
        public bool Left;
        public bool Right;          
    }

    public struct Player : IComponentData
    {
        public float RotationSpeed;
        public float MoveSpeed;
        public float FireRate;
        public float FireSpeed;
        public Entity MissilePrefab;
    }

    public struct Explosion : IComponentData
    {
        public float Timer;
    }

    public struct Missile : IComponentData
    {
    }

    public enum GameStates
    {
        None,
        Start,
        InGame
    }

    public struct GameState : IComponentData
    {
        public GameStates Value;
    }

    public struct HudShowState : IComponentData
    {
        public GameStates Value;
    }

    public struct HudObject : IBufferElementData
    {
        public Entity Value;
    }

    public enum ButtonTypes
    {
        None,
        UpArrow,
        DownArrow,
        LeftArrow,
        RightArrow
    }

    public struct ButtonIdentifier : IComponentData
    {
        public ButtonTypes Value;
    }
    
    public struct ScaleOscillator : IComponentData
    {
        public float PulseSpeed;
        public float PulseSize;
        public float3 BaseScale;
    }

    public struct SpriteSelection : IBufferElementData
    {
        public Entity Value;
    }

    public struct SpriteSelectionNotSetup : IComponentData
    {
    }

    public struct AudioLibrary : IComponentData
    {
    }

    public enum AudioTypes
    {
        None,
        AsteroidExplosionLarge,
        AsteroidExplosionMedium,
        AsteroidExplosionSmall,
        ExtraShip,
        Heartbeat1,
        Heartbeat2,
        PlayerExplosion,
        PlayerFire,
        PlayerThruster,
        SaucerBig,
        SaucerSmall
    }
    
    public struct AudioObject : IBufferElementData
    {
        public AudioTypes Type;
        public Entity Clip;
    }
}