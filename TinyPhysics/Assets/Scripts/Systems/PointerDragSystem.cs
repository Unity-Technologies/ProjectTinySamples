using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace TinyPhysics.Systems
{
    /// <summary>
    ///     Detect when a body is grabbed and move it along the ground
    ///     Use a raycast to find the ground position and place the object above it
    /// </summary>
    public class PointerDragSystem : PointerSystemBase
    {
        private CollisionFilter m_GroundCollisionFilter;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_GroundCollisionFilter = new CollisionFilter
            {
                BelongsTo = 0xffffffff,
                CollidesWith = 1u << 4,     // Target only ground layer
                GroupIndex = 0,
            };
        }

        protected override void OnInputDown(int pointerId, float2 inputPos)
        {
            // Get entity under pointer
            var pointerRaycastHit = GetPointerRaycastHit(inputPos, m_CollisionFilter);
            var pointerEntity = pointerRaycastHit.Entity;

            // Only target entities with Draggable component
            if (pointerEntity != Entity.Null && HasComponent<Draggable>(pointerEntity))
            {
                var draggable = GetComponent<Draggable>(pointerEntity);
                draggable.PointerId = pointerId;
                draggable.IsDragged = true;
                SetComponent(pointerEntity, draggable);
            }
        }

        protected override void OnInputMove(int pointerId, float2 inputPos, float2 inputDelta)
        {
            Entities.ForEach((ref Draggable draggable, ref Translation translation) =>
            {
                if (draggable.IsDragged && draggable.PointerId == pointerId)
                {
                    // Find ground position under pointer
                    var pointerRaycastHit = GetPointerRaycastHit(inputPos, m_GroundCollisionFilter);
                    if (pointerRaycastHit.Entity != Entity.Null)
                    {
                        // Update dragged entity position
                        translation.Value = pointerRaycastHit.Position;
                        translation.Value.y += 0.5f;    // TODO: Get actual distance from ground
                    }
                }
            }).WithoutBurst().Run();
        }

        protected override void OnInputUp(int pointerId, float2 inputPos)
        {
            Entities.ForEach((ref Draggable draggable) =>
            {
                if (draggable.IsDragged && draggable.PointerId == pointerId)
                {
                    draggable.IsDragged = false;
                }
            }).Run();
        }

        protected override void OnInputCanceled(int pointerId)
        {
            Entities.ForEach((ref Draggable draggable) =>
            {
                if (draggable.IsDragged && draggable.PointerId == pointerId)
                {
                    draggable.IsDragged = false;
                }
            }).Run();
        }
    }
}
