using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TinyPhysics.Systems
{
    /// <summary>
    ///     Store gestures for all pointers in order to detect swipes
    ///     Update Swipeable component of entities listening for a swipe
    ///     This system can be extended in order to detect which objects were hit by swipe
    /// </summary>
    public class PointerSwipeSystem : PointerSystemBase
    {
        const int maxInputs = 6;    // 5 touch + mouse
        const double maxSwipeTime = 0.5f;
        const float minSwipeDistance = 50f;

        struct SwipeInput
        {
            public int pointerId;
            public double startTime;
            public float2 startPosition;
        }

        private NativeArray<SwipeInput> swipeInputs;

        protected override void OnCreate()
        {
            base.OnCreate();

            swipeInputs = new NativeArray<SwipeInput>(maxInputs, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            swipeInputs.Dispose();
        }

        protected override void OnInputDown(int pointerId, float2 inputPos)
        {
            swipeInputs[pointerId + 1] = new SwipeInput
            {
                pointerId = pointerId,
                startTime = Time.ElapsedTime,
                startPosition = inputPos
            };
        }

        protected override void OnInputMove(int pointerId, float2 inputPos, float2 inputDelta)
        {

            // We could check here what entities were affected by swipe
        }

        protected override void OnInputUp(int pointerId, float2 inputPos)
        {
            var swipeInput = swipeInputs[pointerId + 1];

            // Swipe needs to be within specified time frame
            var timeDelta = Time.ElapsedTime - swipeInput.startTime;
            if (timeDelta < maxSwipeTime)
            {
                // Swipe needs to be larger than specified distance
                var swipeVector = inputPos - swipeInput.startPosition;
                var swipeDistance = math.length(swipeVector);
                if (swipeDistance > minSwipeDistance)
                {
                    OnSwipe(swipeVector);
                }
            }
        }

        protected override void OnInputCanceled(int pointerId)
        {

        }

        private void OnSwipe(float2 direction)
        {
            // Translate direction into 4-way swipe
            var swipeDirection = SwipeDirection.None;
            var swipeVector = math.normalize(direction);

            if (swipeVector.x > 0.9f)
                swipeDirection = SwipeDirection.Right;
            else if (swipeVector.x < -0.9f)
                swipeDirection = SwipeDirection.Left;
            else if (swipeVector.y > 0.9f)
                swipeDirection = SwipeDirection.Up;
            else if (swipeVector.y < -0.9f)
                swipeDirection = SwipeDirection.Down;
            else return;

            // Inform entities about swipe
            Entities.ForEach((ref Swipeable swipeable) =>
            {
                swipeable.SwipeDirection = swipeDirection;
            }).Run();
        }
    }
}
