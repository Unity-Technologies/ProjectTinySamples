using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Tiny.Input;
using Unity.Tiny.Rendering;
using Unity.U2D.Entities.Physics;

namespace Unity.Spaceship
{
    public class UIInputSystem : JobComponentSystem
    {
        private EntityQuery m_CameraQuery;
        
        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            m_CameraQuery = GetEntityQuery(ComponentType.ReadOnly<CameraMatrices>());
            
            RequireSingletonForUpdate<ActiveInput>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var input = World.GetExistingSystem<InputSystem>();
            var physicsWorld = World.GetExistingSystem<PhysicsWorldSystem>().PhysicsWorld;
            
            var touches = input.IsTouchSupported() ? GetTouches(input) : GetMouseTouch(input);
            if (touches.Length == 0)
            {
                touches.Dispose();
                return inputDeps;
            }

            var cameraMatrices = m_CameraQuery.ToComponentDataArray<CameraMatrices>(Allocator.TempJob);
            for (var i = 0; i < touches.Length; i++)
                touches[i] = CameraUtil.ViewPortPointToNearClipPoint(cameraMatrices[0], touches[i]);

            cameraMatrices.Dispose();

            var buttonPresses = GetButtonPressesFromTouches(ref physicsWorld, touches);

            var activeInput = GetSingleton<ActiveInput>();
            for (var i = 0; i < buttonPresses.Length; i++)
            {
                switch (buttonPresses[i])
                {
                    case ButtonTypes.UpArrow:
                        activeInput.Accelerate = true;
                        break;
                    case ButtonTypes.DownArrow:
                        activeInput.Reverse = true;
                        break;
                    case ButtonTypes.LeftArrow:
                        activeInput.Left = true;
                        break;
                    case ButtonTypes.RightArrow:
                        activeInput.Right = true;
                        break;
                }
            }
            
            SetSingleton(activeInput);

            touches.Dispose();
            buttonPresses.Dispose();
            
            return inputDeps;
        }

        private NativeArray<float2> GetTouches(InputSystem input)
        {
            var touchCount = input.TouchCount();
            if (touchCount > 0)
            {
                var touches = new NativeArray<float2>(touchCount, Allocator.TempJob);
                for (var i = 0; i < touchCount; i++)
                {
                    touches[i] = CameraUtil.ScreenPointToViewportPoint(EntityManager, new float2(input.GetTouch(i).x, input.GetTouch(i).y));
                }

                return touches;
            }

            return new NativeArray<float2>(0, Allocator.TempJob);
        }        

        private NativeArray<float2> GetMouseTouch(InputSystem input)
        {
            if (input.GetMouseButton(0))
            {
                var mousePosition = CameraUtil.ScreenPointToViewportPoint(EntityManager, input.GetInputPosition());
                
                var touches = new NativeArray<float2>(1, Allocator.TempJob);
                touches[0] = mousePosition;
                return touches;
            }

            return new NativeArray<float2>(0, Allocator.TempJob);
        }

        private NativeList<ButtonTypes> GetButtonPressesFromTouches(ref PhysicsWorld physicsWorld, NativeArray<float2> touches)
        {
            var buttonIdentifiers = GetComponentDataFromEntity<ButtonIdentifier>();
            var buttonPresses = new NativeList<ButtonTypes>(Allocator.TempJob);

            for (var i = 0; i < touches.Length; i++)
            {
                var pointInput = new OverlapPointInput()
                {
                    Position = touches[i],
                    Filter = CollisionFilter.Default
                };

                if (physicsWorld.OverlapPoint(pointInput, out var overlapPointHit))
                {
                    var body = physicsWorld.AllBodies[overlapPointHit.PhysicsBodyIndex];
                    if(buttonIdentifiers.HasComponent(body.Entity))
                        buttonPresses.Add(buttonIdentifiers[body.Entity].Value);
                }
            }
            
            return buttonPresses;
        }
    }
}
