using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.U2D.Entities.Physics;

namespace Unity.TinyGems
{
    public class InputToActionSystem : JobComponentSystem
    {
        private EntityQuery m_CellQuery;
        private EntityQuery m_CameraQuery;
        
        private float2 m_StartPosition;
        
        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            m_CameraQuery = GetEntityQuery(typeof(CameraMatrices));
            
            RequireSingletonForUpdate<ActiveInput>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var input = World.GetExistingSystem<Tiny.Input.InputSystem>();
            var physicsWorld = World.GetExistingSystem<PhysicsWorldSystem>().PhysicsWorld;

            if (InputUtil.GetInputDown(input))
            {
                m_StartPosition = CameraUtil.ScreenPointToViewportPoint(EntityManager, InputUtil.GetInputPosition(input));   
            }
            else if (InputUtil.GetInputUp(input))
            {
                var inputPos = CameraUtil.ScreenPointToViewportPoint(EntityManager, InputUtil.GetInputPosition(input));
                var inputDelta = inputPos - m_StartPosition;
                
                var cameraMatrices = m_CameraQuery.ToComponentDataArray<CameraMatrices>(Allocator.TempJob);
                var resultPos = CameraUtil.ViewPortPointToNearClipPoint(cameraMatrices[0], m_StartPosition);

                var activeInput = GetSingleton<ActiveInput>();
                activeInput.Value = GetSwapInputFromWorldPos(ref physicsWorld, resultPos, inputDelta);
                
                SetSingleton(activeInput);

                cameraMatrices.Dispose();
            }

            return inputDeps;
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
