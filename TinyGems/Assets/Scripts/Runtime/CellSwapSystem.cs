using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.TinyGems
{
    public class CellSwapSystem : SystemBase
    {
        private float2 m_MouseStartPos;
        private bool m_IsMouseDown;

        private Entity Find(int2 pos)
        {
            var entity = Entity.Null;
            Entities.ForEach((Entity e, in Cell c) =>
            {
                if(pos.x == c.Position.x && pos.y == c.Position.y)
                    entity = e;
            }).Run();
            return entity;
        }
        
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<GameStateData>();
            RequireSingletonForUpdate<ActiveInput>();
        }
        
        protected override void OnUpdate()
        {
            var gameState = GetSingleton<GameStateData>();
            if (gameState.State != GameState.Swap)
                return;

            var activeInput = GetSingleton<ActiveInput>();
            if (activeInput.Value.Cell == Entity.Null)
                return;

            var cmdBuffer = new EntityCommandBuffer(Allocator.TempJob);
            var inputDelta = activeInput.Value.DeltaInput; 
            var cellA = activeInput.Value.Cell;
            var posA = EntityManager.GetComponentData<Cell>(cellA).Position;

            int2 posB;
            var cellB = Entity.Null;
            
            // what is the other cell
            if (math.abs(inputDelta.x) > math.abs(inputDelta.y))
            {
                if (inputDelta.x > 0) // move right
                {
                    posB = new int2(posA.x + 1, posA.y);
                }
                else // move left
                {
                    posB = new int2(posA.x - 1, posA.y);
                }
            }
            else
            {
                if (inputDelta.y > 0) // move up
                {
                    posB = new int2(posA.x, posA.y + 1);
                }
                else // move down
                {
                    posB = new int2(posA.x, posA.y - 1);
                }
            }

            cellB = Find(posB);
            if (cellB == Entity.Null)
                return;
            
            cmdBuffer.AddComponent(cellA, new Swap
            {
                Position = posB
            });
            cmdBuffer.AddComponent(cellB, new Swap
            {
                Position = posA
            });
            
            gameState.State = GameState.Move;
            SetSingleton(gameState);
            SetSingleton(new ActiveInput());
            
            cmdBuffer.Playback(EntityManager);
            cmdBuffer.Dispose();
        }
    }
}