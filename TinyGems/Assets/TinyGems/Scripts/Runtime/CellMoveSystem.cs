using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.TinyGems
{
    public class CellMoveSystem : ComponentSystem
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

            var somethingMoved = false;
            
            var deltaTime = Time.DeltaTime;
            Entities.ForEach((Entity e, ref Cell c, ref Translation t, ref Swap s) =>
            {
                s.Progress += deltaTime * k_ProgressSpeed;
                if (s.Progress < k_ProgressDonePercentage)
                {
                    somethingMoved = true;
                    var dir = (s.Position - t.Value.xy) * s.Progress;
                    t.Value += new float3(dir, 0);
                }
                else
                {
                    t.Value = new float3(s.Position, 0);
                    PostUpdateCommands.SetComponent(e, new Cell
                    {
                        Position = s.Position,
                        Color = c.Color
                    });
                    PostUpdateCommands.RemoveComponent<Swap>(e);
                }
            });

            if (somethingMoved) 
                return;
            
            gameState.State = GameState.Match;
            SetSingleton(gameState);
        }
    }
}
