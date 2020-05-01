using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TinyAquarium
{
    public class PlayerSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref Player player, ref PlayerInput playerInput, ref Translation translation, ref Rotation rotation) =>
            {
                var oldPosition = translation.Value;
                var newPosition = translation.Value +
                    new float3(0,
                    playerInput.InputAxis.y * player.Speed,
                    -playerInput.InputAxis.x * player.Speed);
                translation.Value = new float3(newPosition.x,
                    // Clamp the player position to avoid going off screen
                    math.clamp(newPosition.y, player.VerticalLimit.y, player.VerticalLimit.x),
                    math.clamp(newPosition.z, player.HorizontalLimit.y, player.HorizontalLimit.x));
                //Update Seahorse Player direction (rotationY) only if moved on Z
                var MoveDirection = oldPosition.z - newPosition.z;
                if (MoveDirection != 0)
                {
                    var rotateAmount = 0f;
                    // If direction < 0 moving left (0) else right (180)
                    rotateAmount = (MoveDirection < 0) ? 0f : 180f;
                    rotation.Value = quaternion.RotateY(math.radians(rotateAmount));
                }
            }).ScheduleParallel();
        }
    }
}
