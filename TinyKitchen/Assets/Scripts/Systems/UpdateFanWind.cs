using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;

namespace TinyKitchen
{
    ///<summary>
    /// Add wind force and animate the fan
    ///</summary>
    public class UpdateFanWind : SystemBase
    {
        protected override void OnUpdate()
        {
            var deltaTime = Time.DeltaTime;

            // Get access to fan component
            var windForce = GetSingleton<FanComponent>().fanForce;
            var windDirection = GetSingleton<FanComponent>().fanHeading;

            // Apply force to the food when it is launched
            Entities.ForEach((Entity entity, ref FoodInstanceComponent food, ref PhysicsVelocity physicsVelocity,
                in PhysicsMass mass) =>
            {
                if (!food.isLaunched)
                    return;

                physicsVelocity.ApplyLinearImpulse(mass, windDirection * windForce * deltaTime);
            }).WithoutBurst().Run();

            var speed = windForce * 32.0f;
            var direction = math.normalize(math.float3(windDirection.x, 0.0f, windDirection.z));

            // Change fan angle
            Entities.ForEach((ref Rotation rot, in FanAnimHeadComponent _) =>
            {
                rot.Value = quaternion.LookRotation(direction, math.up());
            }).ScheduleParallel();

            // Rotate fan blades
            Entities.ForEach((ref Rotation rot, in FanAnimBladesComponent _) =>
            {
                rot.Value = math.mul(rot.Value, quaternion.RotateZ(deltaTime * speed));
            }).ScheduleParallel();
        }
    }
}