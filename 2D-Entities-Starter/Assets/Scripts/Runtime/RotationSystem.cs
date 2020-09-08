using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Tiny2D
{
    public class RotationSystem : SystemBase 
    {
        protected override void OnUpdate()
        {
            var deltaTime = Time.DeltaTime;
             Entities.ForEach((
                Entity e,
                ref Rotation rotation,
                in RotationSpeed rotationSpeed) =>
            {
                var rotationAmount = quaternion.RotateZ(rotationSpeed.Value * deltaTime);
                rotation.Value = math.mul(rotation.Value, rotationAmount);
                
            }).ScheduleParallel();
        }
    }
}