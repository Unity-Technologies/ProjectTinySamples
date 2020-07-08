using Unity.Entities;

namespace TinyPhysics.Systems
{
    /// <summary>
    ///     Open door when player is nearby
    /// </summary>
    [UpdateAfter(typeof(ProximitySystem))]
    public class ElevatorProximitySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref Proximity proximity, ref Elevator elevator) =>
            {
                if (proximity.distanceHit.Entity != Entity.Null)
                {
                    elevator.ElevatorState = ElevatorState.GoingDown;
                }
                else
                    elevator.ElevatorState = ElevatorState.GoingUp;
            }).ScheduleParallel();
        }
    }
}
