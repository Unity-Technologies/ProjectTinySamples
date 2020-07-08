using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;

namespace TinyPhysics.Systems
{
    /// <summary>
    ///     Detect when a jump trigger is set in order to apply a vertical impulse
    /// </summary>
    public class JumpSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float deltaTime = Time.DeltaTime;

            Entities.ForEach((ref PhysicsVelocity velocity, ref PhysicsMass mass, ref Jumper jumper) =>
            {
                if (jumper.JumpTrigger)
                {
                    // Jump by applying an impulse on y Axis
                    velocity.ApplyLinearImpulse(mass, new float3(0, jumper.jumpImpulse, 0));

                    // Consume trigger
                    jumper.JumpTrigger = false;
                }
            }).ScheduleParallel();
        }
    }
}
