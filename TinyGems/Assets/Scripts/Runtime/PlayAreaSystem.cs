using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.TinyGems
{
    public class PlayAreaSystem : SystemBase
    {
        private Scenes m_CurrentScene = Scenes.None;
        private EntityQuery m_BackgroundQuery;
        private EntityQuery m_CellQuery;
        
        protected override void OnCreate()
        {
            base.OnCreate();
            
            RequireSingletonForUpdate<CellManager>();
            RequireSingletonForUpdate<ActiveScene>();

            m_BackgroundQuery = GetEntityQuery(ComponentType.ReadWrite<CellBackground>());
            m_CellQuery = GetEntityQuery(ComponentType.ReadWrite<Cell>());
        }

        protected override void OnUpdate()
        {
            var activeScene = GetSingleton<ActiveScene>();
            if (activeScene.Value == m_CurrentScene)
                return;

            if (activeScene.Value == Scenes.Title)
                DestroyPlayArea();
            else if(activeScene.Value == Scenes.Game)
                SpawnPlayArea();
            
            m_CurrentScene = activeScene.Value;
        }

        private void DestroyPlayArea()
        {
            EntityManager.DestroyEntity(m_BackgroundQuery);
            EntityManager.DestroyEntity(m_CellQuery);
        }

        private void SpawnPlayArea()
        {
            var cmdBuffer = new EntityCommandBuffer(Allocator.TempJob);
            var cellManager = GetSingleton<CellManager>();

            for (var x = 0; x < cellManager.MaxCol; x++)
            {
                for(var y = 0; y < cellManager.MaxRow; y++)
                {
                    var backgroundEntity = cmdBuffer.Instantiate(cellManager.CellBackground);
    
                    cmdBuffer.AddComponent<CellBackground>(backgroundEntity);
                    
                    cmdBuffer.SetComponent(backgroundEntity, new Translation
                    {
                        Value = new float3(x, y, 0)
                    });
                }
            }
            
            cmdBuffer.Playback(EntityManager);
            cmdBuffer.Dispose();
        }
    }
}
