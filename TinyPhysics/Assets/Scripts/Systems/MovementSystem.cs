using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;

namespace TinyPhysics.Systems
{
    /// <summary>
    ///     Use the movement direction vector to apply a force to a body
    /// </summary>
    public class MovementSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float deltaTime = Time.DeltaTime;

            Entities.ForEach((ref Moveable moveable, ref PhysicsVelocity velocity, ref PhysicsMass mass, ref Rotation rotation, ref LocalToWorld localToWorld) =>
            {
                if (moveable.moveDirection.x != 0 || moveable.moveDirection.z != 0)
                {
                    // Move by applying a force
                    velocity.ApplyLinearImpulse(mass, moveable.moveDirection * moveable.moveForce * deltaTime);

                    // Look where you're going
                    rotation.Value = quaternion.LookRotation(moveable.moveDirection, localToWorld.Up);
                }
            }).ScheduleParallel();
        }
    }
}
