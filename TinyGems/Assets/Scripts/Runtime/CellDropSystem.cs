using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Unity.TinyGems
{
    public class CellDropSystem : SystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<GameStateData>();
            RequireSingletonForUpdate<CellManager>();
        }

        protected override void OnUpdate()
         {
             var gameState = GetSingleton<GameStateData>();
             if (gameState.State != GameState.Drop)
                 return;
             
             var cellManager = GetSingleton<CellManager>();
             var cmdBuffer = new EntityCommandBuffer(Allocator.TempJob);
             
             for (var x = 0; x < cellManager.MaxCol; x++)
             {
                 var jobResult = new NativeList<CellInfo>(Allocator.Temp);
                 Entities.ForEach((
                     in Entity entity, 
                     in Cell cell) =>
                 {
                     CellUtil.AddCellAtColumn(entity, cell, x, ref jobResult);
                 }).Run();
                 
                 var cells = jobResult.AsArray();
                 cells.Sort(new ByCol());
                 
                 var target = 0;
                 foreach (var c in cells)
                 {
                     var deltaY = c.Cell.Position.y - target;
                     if (deltaY > 0)
                     {
                         // the check is here instead of in the query, because
                         // we need to count properly even while it is moving
                         // but we don't want to refresh the swap component
                         if (EntityManager.HasComponent<Swap>(c.Entity))
                             continue;
                         
                         cmdBuffer.AddComponent(c.Entity, new Swap
                         {
                             Position = new int2(c.Cell.Position.x, target)
                         });
                     }
                     target++;
                 }
             }
             
             gameState.State = GameState.Move;
             SetSingleton(gameState);
             
             cmdBuffer.Playback(EntityManager);
             cmdBuffer.Dispose();
         }
    }
}