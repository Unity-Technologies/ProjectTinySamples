using Unity.Entities;
using Unity.Mathematics;


namespace BlendShapeDemo
{
    /// <summary>
    /// Detect when an object is tapped and update its Tappable component
    /// A tap is defined as a Down/Up gesture within a certain time and distance
    /// </summary>

    public class PointerTapSystem : PointerSystemBase
    {
        const float tapSqrDistanceThreshold = 100f;
        const float tapTimeThreshold = 0.5f;

        private Game game;

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            collisionFilter = new Unity.Physics.CollisionFilter
            {
                BelongsTo = 1u << 30,           // Set raycast to Input category
                CollidesWith = 1u << 31,        // Target UI
                GroupIndex = 0,
            };
        }
        protected override void OnInputDown(int pointerId, float2 inputPos)
        {    
            // Get entity under pointer
            var pointerRaycastHit = GetPointerRaycastHit(inputPos, collisionFilter);
            var pointerEntity = pointerRaycastHit.Entity;

            // Only target entities with Tappable component
            if (pointerEntity != Entity.Null && HasComponent<Tappable>(pointerEntity))
            {
                // Set tap data on entity that was pressed
                var tappable = GetComponent<Tappable>(pointerEntity);
                tappable.PointerId = pointerId;
                tappable.TimePressed = Time.ElapsedTime;
                tappable.PointerMoveSqrDistance = 0;
                tappable.IsPressed = true;
                tappable.IsTapped = false;
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
            }).WithStructuralChanges().Run();
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
