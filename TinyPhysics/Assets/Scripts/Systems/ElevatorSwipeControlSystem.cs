using Unity.Entities;

namespace TinyPhysics.Systems
{
    /// <summary>
    ///     Listen for swipes in order to set elevator state
    /// </summary>
    [UpdateAfter(typeof(PointerSwipeSystem))]
    public class ElevatorSwipeControlSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref Swipeable swipeable, ref Elevator elevator) =>
            {
                // Going up
                if (swipeable.SwipeDirection == SwipeDirection.Up)
                {
                    elevator.ElevatorState = ElevatorState.GoingUp;
                }

                // Going down
                else if (swipeable.SwipeDirection == SwipeDirection.Down)
                {
                    elevator.ElevatorState = ElevatorState.GoingDown;
                }

                // Consume swipe
                swipeable.SwipeDirection = SwipeDirection.None;
            }).ScheduleParallel();
        }
    }
}
