using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Rotate entities that have the Rotator component.
    /// </summary>
    public class Rotate : SystemBase
    {
        protected override void OnUpdate()
        {
            var deltaTime = Time.DeltaTime;

            Entities.ForEach((ref Rotator rotator, ref Rotation rotation) =>
            {
                var rotateAmount = rotator.RotateSpeed * deltaTime;
                rotation.Value = math.mul(rotation.Value, quaternion.RotateY(math.radians(rotateAmount)));
            }).ScheduleParallel();
        }
    }
}
