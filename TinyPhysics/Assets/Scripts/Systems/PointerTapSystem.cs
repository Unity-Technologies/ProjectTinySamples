using Unity.Entities;
using Unity.Mathematics;

namespace TinyPhysics.Systems
{
    /// <summary>
    ///     Detect when an object is tapped and update its Tappable component
    ///     A tap is defined as a Down/Up gesture within a certain time and distance
    /// </summary>
    public class PointerTapSystem : PointerSystemBase
    {
        const float tapSqrDistanceThreshold = 100f;
        const float tapTimeThreshold = 0.5f;

        protected override void OnInputDown(int pointerId, float2 inputPos)
        {
            // Get entity under pointer
            var pointerRaycastHit = GetPointerRaycastHit(inputPos, m_CollisionFilter);
            var pointerEntity = pointerRaycastHit.Entity;

            // Only target entities with Tappable component
            if (pointerEntity != Entity.Null && HasComponent<Tappable>(pointerEntity))
            {
                // Set tap data on entity that was pressed
                var tappable = new Tappable
                {
                    PointerId = pointerId,
                    TimePressed = Time.ElapsedTime,
                    IsPressed = true
                };

                SetComponent(pointerEntity, tappable);
            }
        }

        protected override void OnInputMove(int pointerId, float2 inputPos, float2 inputDelta)
        {
            Entities.ForEach((ref Tappable tappable) =>
            {
                if (tappable.PointerId == pointerId && tappable.IsPressed)
                {
                    // Update total distance that pointer moved
                    tappable.PointerMoveSqrDistance += math.dot(inputDelta, inputDelta);
                }
            }).Run();
        }

        protected override void OnInputUp(int pointerId, float2 inputPos) {
            Entities.ForEach((ref Tappable tappable) =>
            {
                if (tappable.PointerId == pointerId && tappable.IsPressed)
                {
                    // Consider a tap only if pointer didn't move much and duration was short
                    var isWithinTimeThreshold = Time.ElapsedTime - tappable.TimePressed < tapTimeThreshold;
                    var isWithinDistanceThreshold = tappable.PointerMoveSqrDistance < tapSqrDistanceThreshold;
                    tappable.IsTapped = isWithinTimeThreshold && isWithinDistanceThreshold;
                    tappable.IsPressed = false;
                }
            }).WithoutBurst().Run();
        }

        protected override void OnInputCanceled(int pointerId) {
            Entities.ForEach((ref Tappable tappable) =>
            {
                if (tappable.PointerId == pointerId)
                {
                    tappable.IsPressed = false;
                    tappable.IsTapped = false;
                }
            }).Run();
        }
    }
}
