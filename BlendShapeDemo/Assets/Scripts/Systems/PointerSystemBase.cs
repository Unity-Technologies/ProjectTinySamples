using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Tiny;
using Unity.Tiny.Input;
using Unity.Tiny.Rendering;

namespace BlendShapeDemo
{
    /// <summary>
    /// Translate mouse and touch inputs into overrideable functions
    /// Create a convenience function to detect object under pointer
    /// </summary>

    public abstract class PointerSystemBase : SystemBase
    {
        protected InputSystem inputSystem;
        protected ScreenToWorld screenToWorld;

        protected CollisionFilter collisionFilter;
        protected float rayDistance = 100f;

        protected override void OnCreate()
        {
            inputSystem = World.GetExistingSystem<InputSystem>();
            screenToWorld = World.GetExistingSystem<ScreenToWorld>();

            // Category names are defined in Assets/PhysicsCategoryNames and assgined to PhysicsShape components in Editor
            collisionFilter = new CollisionFilter
            {
                BelongsTo = 1u << 30,           // Set raycast to Input category
                CollidesWith = 0xffffffff,      // Target everything
                GroupIndex = 0,
            };
        }

        protected override void OnUpdate()
        {
            // Touch
            if (inputSystem.IsTouchSupported() && inputSystem.TouchCount() > 0)
            {
                for (var i = 0; i < inputSystem.TouchCount(); i++)
                {
                    var touch = inputSystem.GetTouch(i);
                    var inputPos = new float2(touch.x, touch.y);

                    switch (touch.phase)
                    {
                        case TouchState.Began:
                            OnInputDown(i, inputPos);
                            break;

                        case TouchState.Moved:
                            OnInputMove(i, inputPos, new float2(touch.deltaX, touch.deltaY));
                            break;

                        case TouchState.Stationary:
                            OnInputMove(i, inputPos, new float2(touch.deltaX, touch.deltaY));
                            break;

                        case TouchState.Ended:
                            OnInputUp(i, inputPos);
                            break;

                        case TouchState.Canceled:
                            OnInputCanceled(i);
                            break;
                    }
                }
            }

            // Mouse
            else if (inputSystem.IsMousePresent())
            {
                var inputPos = inputSystem.GetInputPosition();

                if (inputSystem.GetMouseButtonDown(0))
                {
                    OnInputDown(-1, inputPos);
                }
                else if (inputSystem.GetMouseButton(0))
                {
                    OnInputMove(-1, inputPos, inputSystem.GetInputDelta());
                }
                else if (inputSystem.GetMouseButtonUp(0))
                {
                    OnInputUp(-1, inputPos);
                }
            }
        }

        protected abstract void OnInputDown(int pointerId, float2 inputPos);

        protected abstract void OnInputMove(int pointerId, float2 inputPos, float2 inputDelta);

        protected abstract void OnInputUp(int pointerId, float2 inputPos);

        protected abstract void OnInputCanceled(int pointerId);

        protected RaycastHit GetPointerRaycastHit(float2 inputPos, CollisionFilter collisionFilter)
        {
            ref PhysicsWorld physicsWorld = ref World.DefaultGameObjectInjectionWorld.GetExistingSystem<BuildPhysicsWorld>().PhysicsWorld;

            // Convert input position to ray going from screen to world
            float3 rayOrigin, rayDirection;
            screenToWorld.InputPosToWorldSpaceRay(inputPos, out rayOrigin, out rayDirection);
            var RaycastInput = new RaycastInput
            {
                Start = rayOrigin,
                End = rayOrigin + rayDirection * rayDistance,
                Filter = collisionFilter
            };

            // Return top-most entity that was hit by ray
            physicsWorld.CastRay(RaycastInput, out RaycastHit hit);
            return hit;
        }

        protected float2 GetNormalizedInputPos(float2 inputPos)
        {
            var di = GetSingleton<DisplayInfo>();
            return new float2(inputPos.x / di.width, inputPos.y / di.height);
        }
    }
}
