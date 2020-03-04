using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.Transforms;

namespace Unity.TinyGems
{
    public class HeartSystem : ComponentSystem
    {
        private Scenes m_CurrentScene = Scenes.None;
        private int m_NoOfHearts = -1;
        private EntityQuery m_BackgroundQuery;
        
        protected override void OnCreate()
        {
            base.OnCreate();
            
            RequireSingletonForUpdate<HeartManager>();
            RequireSingletonForUpdate<ActiveScene>();
            RequireSingletonForUpdate<GameStateData>();
            RequireSingletonForUpdate<Camera>();

            m_BackgroundQuery = GetEntityQuery(typeof(HeartBackground));
        }

        protected override void OnUpdate()
        {
            var activeScene = GetSingleton<ActiveScene>();
            
            if (activeScene.Value != m_CurrentScene)
                SceneChangeUpdate(activeScene);
            else if(activeScene.Value == Scenes.Game)
                HeartReduction();
        }

        private void SceneChangeUpdate(ActiveScene activeScene)
        {
            if (activeScene.Value == Scenes.Title)
                DestroyHearts();
            else if(activeScene.Value == Scenes.Game)
                SpawnHearts();
            
            m_CurrentScene = activeScene.Value;
        }

        private void DestroyHearts()
        {
            PostUpdateCommands.DestroyEntity(m_BackgroundQuery);
         
            var heartManagerEntity = GetSingletonEntity<HeartManager>();
            var heartBuffers = GetBufferFromEntity<Heart>();
            if (!heartBuffers.Exists(heartManagerEntity))
                return;
            
            var hearts = heartBuffers[heartManagerEntity];
            
            for(var i = 0; i < hearts.Length; i++)
                PostUpdateCommands.DestroyEntity(hearts[i].Value);
            hearts.Clear();
        }

        private void SpawnHearts()
        {
            var gameState = GetSingleton<GameStateData>();
            var heartManager = GetSingleton<HeartManager>();
            var heartManagerEntity = GetSingletonEntity<HeartManager>();
            
            gameState.Hearts = heartManager.NoOfHearts;
            SetSingleton(gameState);
            
            var camEntity = GetSingletonEntity<Camera>();
            var camTr = EntityManager.GetComponentData<Translation>(camEntity);
            var cam = GetSingleton<Camera>();
            var top = cam.fov - 0.5f;
            var right = top * cam.aspect;
            var heartAnchor = new float3(right + camTr.Value.x, top + camTr.Value.y, 0);

            var heartBuffer = PostUpdateCommands.AddBuffer<Heart>(heartManagerEntity);
            
            for (var i = 0; i < gameState.Hearts; i++)
            {
                var translation = new Translation
                {
                    Value = heartAnchor
                };
                
                var heartBackground = PostUpdateCommands.Instantiate(heartManager.HeartBackgroundPrefab);
                var heart = PostUpdateCommands.Instantiate(heartManager.HeartPrefab);
                
                PostUpdateCommands.SetComponent(heartBackground, translation);
                PostUpdateCommands.SetComponent(heart, translation);
                
                PostUpdateCommands.AddComponent(heartBackground, typeof(HeartBackground));

                heartBuffer.Add(new Heart()
                {
                    Value = heart
                });

                heartAnchor.x--;
            }

            m_NoOfHearts = gameState.Hearts;
        }

        private void HeartReduction()
        {
            var gameState = GetSingleton<GameStateData>();
            if (gameState.Hearts == m_NoOfHearts)
                return;

            var heartManagerEntity = GetSingletonEntity<HeartManager>();
            var heartBuffers = GetBufferFromEntity<Heart>();

            var hearts = heartBuffers[heartManagerEntity];
            PostUpdateCommands.AddComponent<Disabled>(hearts[gameState.Hearts].Value);
            
            m_NoOfHearts = gameState.Hearts;
        }
    }
}