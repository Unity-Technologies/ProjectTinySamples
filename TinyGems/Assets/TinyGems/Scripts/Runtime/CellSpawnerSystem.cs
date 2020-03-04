using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.U2D.Entities;

using System.Collections.Generic;

namespace Unity.TinyGems
{
    struct CellInfo
    {
        public Entity Entity;
        public Cell Cell;
    }

    class ByCol : IComparer<CellInfo>
    {
        public int Compare(CellInfo x, CellInfo y)
        {
            return x.Cell.Position.y - y.Cell.Position.y;
        }
    }
    
    class ByRow : IComparer<CellInfo>
    {
        public int Compare(CellInfo x, CellInfo y)
        {
            return x.Cell.Position.x - y.Cell.Position.x;
        }
    }
    
    static class CellUtil
    {
        public static NativeArray<CellInfo> GetCellEntitiesAtColumn(this EntityQueryBuilder eqb, int i)
        {
            var result = new NativeList<CellInfo>(Allocator.Temp);
            
            eqb.ForEach((Entity e, ref Cell c) =>
            {
                if(c.Position.x == i)
                    result.Add(new CellInfo
                    {
                        Entity = e, Cell = c
                    });
            });
            
            return result.AsArray();
        }
        
        public static NativeArray<CellInfo> GetCellEntitiesAtRow(this EntityQueryBuilder eqb, int i)
        {
            var result = new NativeList<CellInfo>(Allocator.Temp);
            
            eqb.ForEach((Entity e, ref Cell c) =>
            {
                if(c.Position.y == i)
                    result.Add(new CellInfo
                    {
                        Entity = e, Cell = c
                    });
            });

            return result.AsArray();
        }
    }
    
    class CellSpawnerSystem : ComponentSystem
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
            
            var cellManagerEntity = GetSingletonEntity<CellManager>();
            var cellManager = GetSingleton<CellManager>();
            var cellSprites = EntityManager.GetBuffer<CellSprite>(cellManagerEntity);
            var cellPrefabSR = EntityManager.GetComponentData<SpriteRenderer>(cellManager.CellPrefab);
            
            // look at every column and spawn if it's less than minimum
            for (var x = 0; x < cellManager.MaxCol; x++)
            {
                var results = Entities.GetCellEntitiesAtColumn(x);
                var needed = cellManager.MaxRow - results.Length;

                // spawn
                for(var i = 0; i < needed; i++)
                {
                    var y = i + cellManager.MaxRow;

                    var cell = PostUpdateCommands.Instantiate(cellManager.CellPrefab);
            
                    PostUpdateCommands.SetComponent(cell, new Translation
                    {
                        Value = new float3(x, y, 0)
                    });

                    var color = m_Random.NextInt(cellSprites.Length);
                    PostUpdateCommands.AddComponent(cell, new Cell
                    {
                        Position = new int2(x, y),
                        Color = color
                    });

                    cellPrefabSR.Sprite = cellSprites[color].Sprite;
                    PostUpdateCommands.SetComponent(cell, cellPrefabSR);  
                }
            }

            gameState.State = GameState.Drop;
            SetSingleton(gameState);
        }
    }
}