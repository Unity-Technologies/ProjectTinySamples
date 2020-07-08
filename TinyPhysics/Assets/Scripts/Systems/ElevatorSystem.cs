using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace TinyPhysics.Systems
{
    /// <summary>
    ///     Move elevator and stop when it reaches destination
    /// </summary>
    public class ElevatorSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref Elevator elevator, ref Translation translation, ref PhysicsVelocity velocity) =>
            {
                switch(elevator.ElevatorState)
                {
                    case ElevatorState.GoingUp:

                        // Going up
                        if (translation.Value.y < elevator.raisedPosition)
                        {
                            velocity.Linear = new float3(0, elevator.moveSpeed, 0);
                        }

                        // Stop elevator at top
                        else
                        {
                            velocity.Linear = float3.zero;
                            elevator.ElevatorState = ElevatorState.Stopped;
                            translation.Value = new float3(translation.Value.x, elevator.raisedPosition, translation.Value.z);
                        }

                        break;

                    case ElevatorState.GoingDown:

                        // Going down
                        if (translation.Value.y > elevator.loweredPosition)
                        {
                            velocity.Linear = new float3(0, -elevator.moveSpeed, 0);
                        }

                        // Stop elevator at bottom
                        else
                        {
                            velocity.Linear = float3.zero;
                            elevator.ElevatorState = ElevatorState.Stopped;
                            translation.Value = new float3(translation.Value.x, elevator.loweredPosition, translation.Value.z);
                        }

                        break;
                }
            }).ScheduleParallel();
        }
    }
}
