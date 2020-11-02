using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.Transforms;
using Unity.Tiny;
using Unity.U2D.Entities.Physics;

namespace Unity.Spaceship
{
    [UpdateBefore(typeof(TransformSystemGroup))]
    class AsteroidSpawnSystem : ComponentSystem
    {
        private float m_ElapsedTime;
        private Random m_Random;
        
        protected override void OnCreate()
        {
            base.OnCreate();
            
            RequireSingletonForUpdate<GameState>();
            RequireSingletonForUpdate<AsteroidSpawner>();
            RequireSingletonForUpdate<Camera>();
            
            m_Random = new Random(314159);
        }

        protected override void OnUpdate()
        {
            var gameState = GetSingleton<GameState>();
            if (gameState.Value != GameStates.InGame)
                return;
            
            m_ElapsedTime += Time.DeltaTime;
            
            var settings = GetSingleton<AsteroidSpawner>();
            var settingsEntity = GetSingletonEntity<AsteroidSpawner>();
            var asteroidSprites = EntityManager.GetBuffer<AsteroidSprite>(settingsEntity);
            
            // if (settings.Rate < 1)
            var timeLimit = 1.0f / settings.Rate;
            var camera = GetSingleton<Camera>();

            var srPrefab = EntityManager.GetComponentData<SpriteRenderer>(settings.Prefab);
            
            if(m_ElapsedTime > timeLimit)
            {
                // world view
                var x = camera.aspect * camera.fov;
                var y = camera.fov;

                // find a point somewhere outside of view,
                var rot = quaternion.RotateZ(m_Random.NextFloat(2 * math.PI));
                var pos = new float3(x, y, 0);
                pos = math.mul(rot, pos);
                
                // aim it directly at the camera's position
                var dir = math.normalize(float3.zero - pos) * m_Random.NextFloat(settings.MinSpeed, settings.MaxSpeed);
                
                // vary the aim by a a little to miss the center
                rot = quaternion.RotateZ(m_Random.NextFloat(settings.PathVariation * 2 * math.PI)); // 10% variation
                // vary the speed as well
                dir = math.mul(rot, dir);
                
                var ast = PostUpdateCommands.Instantiate(settings.Prefab);

                var astType = m_Random.NextInt(asteroidSprites.Length);
                srPrefab.Sprite = asteroidSprites[astType].Sprite; 
                PostUpdateCommands.SetComponent(ast,srPrefab);              

                PostUpdateCommands.SetComponent( ast, new PhysicsVelocity
                {
                    Linear = new float2(dir.x, dir.y)
                });
                
                PostUpdateCommands.SetComponent(ast, new Translation
                {
                    Value = pos
                });

                m_ElapsedTime = 0;
            }
        }  
    }
}