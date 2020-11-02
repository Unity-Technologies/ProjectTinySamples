using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

namespace Unity.TinyGems
{
    public class CellMoveSystem : SystemBase
    {
        private const float k_ProgressSpeed = 1.5f;
        private const float k_ProgressDonePercentage = 0.8f;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<GameStateData>();
        }

        protected override void OnUpdate()
        {
            var gameState = GetSingleton<GameStateData>();
            if (gameState.State != GameState.Move)
                return;

            var cmdBuffer = new EntityCommandBuffer(Allocator.TempJob);
            var somethingMoved = false;
            
            var deltaTime = Time.DeltaTime;
            Entities.ForEach((
                ref Swap swap,
                ref Translation translation,
                in Entity entity,
                in Cell cell) =>
            {
                swap.Progress += deltaTime * k_ProgressSpeed;
                if (swap.Progress < k_ProgressDonePercentage)
                {
                    somethingMoved = true;
                    var dir = (swap.Position - translation.Value.xy) * swap.Progress;
                    translation.Value += new float3(dir, 0);
                }
                else
                {
                    translation.Value = new float3(swap.Position, 0);
                    cmdBuffer.SetComponent(entity, new Cell
                    {
                        Position = swap.Position,
                        Color = cell.Color
                    });
                    cmdBuffer.RemoveComponent<Swap>(entity);
                }
            }).Run();

            if (!somethingMoved)
            {
                gameState.State = GameState.Match;
                SetSingleton(gameState);                
            }
            
            cmdBuffer.Playback(EntityManager);
            cmdBuffer.Dispose();
        }
    }
}
