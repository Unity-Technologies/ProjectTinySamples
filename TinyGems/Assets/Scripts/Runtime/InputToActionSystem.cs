using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.U2D.Entities.Physics;

namespace Unity.TinyGems
{
    public class InputToActionSystem : SystemBase
    {
        private EntityQuery m_CellQuery;
        private float2 m_StartPosition;

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            RequireSingletonForUpdate<GameStateData>();
            RequireSingletonForUpdate<ActiveInput>();
        }

        protected override void OnUpdate()
        {
            var gameState = GetSingleton<GameStateData>();
            if (gameState.State != GameState.Swap)
                return;
            
            var input = World.GetExistingSystem<Tiny.Input.InputSystem>();
            var physicsWorld = World.GetExistingSystem<PhysicsWorldSystem>().PhysicsWorld;

            if (InputUtil.GetInputDown(input))
            {
                m_StartPosition = CameraUtil.ScreenPointToWorldPoint(World, InputUtil.GetInputPosition(input));   
            }
            else if (InputUtil.GetInputUp(input))
            {
                var endPosition = CameraUtil.ScreenPointToWorldPoint(World, InputUtil.GetInputPosition(input));
                var inputDelta = endPosition - m_StartPosition;

                var activeInput = GetSingleton<ActiveInput>();
                activeInput.Value = GetSwapInputFromWorldPos(ref physicsWorld, m_StartPosition, inputDelta);
                
                SetSingleton(activeInput);
            }
        }

        private static SwapInput GetSwapInputFromWorldPos(ref PhysicsWorld physicsWorld, float2 worldPos, float2 deltaInput)
        {
            var pointInput = new OverlapPointInput()
            {
                Position = worldPos,
                Filter = CollisionFilter.Default
            };

            var swapInput = new SwapInput()
            {
                DeltaInput = deltaInput
            };

            if (physicsWorld.OverlapPoint(pointInput, out var overlapPointHit))
            {
                var body = physicsWorld.AllBodies[overlapPointHit.PhysicsBodyIndex];
                swapInput.Cell = body.Entity;
            }

            return swapInput;
        }
    }
}
