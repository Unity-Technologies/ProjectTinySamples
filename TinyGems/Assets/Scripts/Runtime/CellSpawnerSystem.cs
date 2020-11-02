using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Tiny;
using System.Collections.Generic;

namespace Unity.TinyGems
{
    public struct CellInfo
    {
        public Entity Entity;
        public Cell Cell;
    }

    public class ByCol : IComparer<CellInfo>
    {
        public int Compare(CellInfo x, CellInfo y)
        {
            return x.Cell.Position.y - y.Cell.Position.y;
        }
    }
    
    public class ByRow : IComparer<CellInfo>
    {
        public int Compare(CellInfo x, CellInfo y)
        {
            return x.Cell.Position.x - y.Cell.Position.x;
        }
    }

    public static class CellUtil
    {
        public static void AddCellAtColumn(in Entity cellEntity, in Cell cell, int column, ref NativeList<CellInfo> result)
        {
            if (cell.Position.x != column)
                return;
                    
            result.Add(new CellInfo
            {
                Entity = cellEntity, 
                Cell = cell
            });            
        }
        
        public static void AddCellAtRow(in Entity cellEntity, in Cell cell, int row, ref NativeList<CellInfo> result)
        {
            if (cell.Position.y != row)
                return;
                    
            result.Add(new CellInfo
            {
                Entity = cellEntity, 
                Cell = cell
            });            
        }
    }
    
    public class CellSpawnerSystem : SystemBase
    {
        private Random m_Random;
        
        protected override void OnCreate()
        {
            m_Random = new Random(314156);
            
            RequireSingletonForUpdate<GameStateData>();
            RequireSingletonForUpdate<CellManager>();
        }

        protected override void OnUpdate()
        {
            var gameState = GetSingleton<GameStateData>();
            if (gameState.State != GameState.Spawn)
                return;

            var cmdBuffer = new EntityCommandBuffer(Allocator.TempJob);
            var cellManagerEntity = GetSingletonEntity<CellManager>();
            var cellManager = GetSingleton<CellManager>();
            var cellSprites = EntityManager.GetBuffer<CellSprite>(cellManagerEntity);
            var cellPrefabSR = EntityManager.GetComponentData<SpriteRenderer>(cellManager.CellPrefab);
            
            // look at every column and spawn if it's less than minimum
            for (var x = 0; x < cellManager.MaxCol; x++)
            {
                var jobResult = new NativeList<CellInfo>(Allocator.Temp);
                Entities.ForEach((
                    in Entity entity, 
                    in Cell cell) =>
                {
                    CellUtil.AddCellAtColumn(entity, cell, x, ref jobResult);
                }).Run();
                
                var needed = cellManager.MaxRow - jobResult.Length;

                // spawn
                for(var i = 0; i < needed; i++)
                {
                    var y = i + cellManager.MaxRow;

                    var cell = cmdBuffer.Instantiate(cellManager.CellPrefab);
            
                    cmdBuffer.SetComponent(cell, new Translation
                    {
                        Value = new float3(x, y, 0)
                    });

                    var color = m_Random.NextInt(cellSprites.Length);
                    cmdBuffer.AddComponent(cell, new Cell
                    {
                        Position = new int2(x, y),
                        Color = color
                    });

                    cellPrefabSR.Sprite = cellSprites[color].Sprite;
                    cmdBuffer.SetComponent(cell, cellPrefabSR);  
                }
            }

            gameState.State = GameState.Drop;
            SetSingleton(gameState);
            
            cmdBuffer.Playback(EntityManager);
            cmdBuffer.Dispose();
        }
    }
}