using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.Transforms;

namespace TinyPhysics.Systems
{
    /// <summary>
    ///     Updates the VirtualJoystick component of entities with a vector defining the distance and direction from its center
    /// </summary>
    public class VirtualJoystickSystem : PointerSystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();

            m_RayDistance = 10;
            m_CollisionFilter.CollidesWith = 1u << 31;     // Target only UI layer
        }

        protected override void OnInputDown(int pointerId, float2 inputPos)
        {
            // Get entity under pointer
            var pointerRaycastHit = GetPointerRaycastHit(inputPos, m_CollisionFilter);
            var pointerEntity = pointerRaycastHit.Entity;

            // Only target entities with VirtualJoystick component
            if (HasComponent<VirtualJoystick>(pointerEntity))
            {
                var virtualJoystick = GetComponent<VirtualJoystick>(pointerEntity);

                // Another pointer is already using this joystick
                if (virtualJoystick.IsPressed)
                    return;

                // Update pointer info
                virtualJoystick.IsPressed = true;
                virtualJoystick.PointerId = pointerId;
                virtualJoystick.Value = float2.zero;

                // Store center point of virtual joystick
                var localToWorld = GetComponent<LocalToWorld>(pointerEntity);
                var screenPosition = m_ScreenToWorld.WorldSpaceToScreenSpace(localToWorld.Position, ScreenToWorldId.MainCamera);
                virtualJoystick.Center = new float2(screenPosition.x, screenPosition.y);

                SetComponent(pointerEntity, virtualJoystick);
            }
        }

        protected override void OnInputMove(int pointerId, float2 inputPos, float2 inputDelta)
        {
            Entities.ForEach((ref VirtualJoystick virtualJoystick) =>
            {
                if (!virtualJoystick.IsPressed || virtualJoystick.PointerId != pointerId)
                    return;

                // Calculate joystick vector
                virtualJoystick.Value = (inputPos - virtualJoystick.Center) / virtualJoystick.inputRadius;

                // Clamp vector inside circle
                float sqrMagnitude = math.lengthsq(virtualJoystick.Value);
                if (sqrMagnitude > 1)
                    virtualJoystick.Value = math.normalize(virtualJoystick.Value);

                // Move virtual joystick knob
                var knobTranslation = GetComponent<Translation>(virtualJoystick.knob);
                knobTranslation.Value = new float3(virtualJoystick.Value.x / 2f, virtualJoystick.Value.y / 2f, knobTranslation.Value.z);
                SetComponent(virtualJoystick.knob, knobTranslation);
            }).Run();
        }

        protected override void OnInputUp(int pointerId, float2 inputPos)
        {
            OnInputCanceled(pointerId);
        }

        protected override void OnInputCanceled(int pointerId)
        {
            Entities.ForEach((ref VirtualJoystick virtualJoystick) =>
            {
                if (!virtualJoystick.IsPressed || virtualJoystick.PointerId != pointerId)
                    return;

                // Calculate joystick vector
                virtualJoystick.IsPressed = false;
                virtualJoystick.Value = float2.zero;

                // Reset virtual joystick knob
                var knobTranslation = GetComponent<Translation>(virtualJoystick.knob);
                knobTranslation.Value = float3.zero;
                SetComponent(virtualJoystick.knob, knobTranslation);
            }).Run();
        }
    }
}
