using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.Transforms;

namespace Unity.TinyGems
{
    public class HeartSystem : SystemBase
    {
        private Scenes m_CurrentScene = Scenes.None;
        private int m_NoOfHearts = -1;
        private EntityQuery m_BackgroundQuery;
        private EntityCommandBuffer m_CmdBuffer;
        
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
            m_CmdBuffer = new EntityCommandBuffer(Allocator.TempJob);
            
            if (activeScene.Value != m_CurrentScene)
                SceneChangeUpdate(activeScene);
            else if(activeScene.Value == Scenes.Game)
                HeartReduction();
            
            m_CmdBuffer.Playback(EntityManager);
            m_CmdBuffer.Dispose();
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
            m_CmdBuffer.DestroyEntity(m_BackgroundQuery);
         
            var heartManagerEntity = GetSingletonEntity<HeartManager>();
            var heartBuffers = GetBufferFromEntity<Heart>();
            if (!heartBuffers.HasComponent(heartManagerEntity))
                return;
            
            var hearts = heartBuffers[heartManagerEntity];
            
            for(var i = 0; i < hearts.Length; i++)
                m_CmdBuffer.DestroyEntity(hearts[i].Value);
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

            var heartBuffer = m_CmdBuffer.AddBuffer<Heart>(heartManagerEntity);
            
            for (var i = 0; i < gameState.Hearts; i++)
            {
                var translation = new Translation
                {
                    Value = heartAnchor
                };
                
                var heartBackground = m_CmdBuffer.Instantiate(heartManager.HeartBackgroundPrefab);
                var heart = m_CmdBuffer.Instantiate(heartManager.HeartPrefab);
                
                m_CmdBuffer.SetComponent(heartBackground, translation);
                m_CmdBuffer.SetComponent(heart, translation);
                
                m_CmdBuffer.AddComponent(heartBackground, typeof(HeartBackground));

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
            m_CmdBuffer.AddComponent<Disabled>(hearts[gameState.Hearts].Value);
            
            m_NoOfHearts = gameState.Hearts;
        }
    }
}