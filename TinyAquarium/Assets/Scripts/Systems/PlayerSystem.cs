using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TinyAquarium
{
    public class PlayerSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref Player player, ref PlayerInput playerInput, ref Translation translation) =>
            {
                var newPosition = translation.Value +
                    new float3(0,
                    playerInput.InputAxis.y * player.Speed,
                    -playerInput.InputAxis.x * player.Speed);
                translation.Value = new float3(newPosition.x,
                    // Clamp the player position to avoid going off screen
                    math.clamp(newPosition.y, player.VerticalLimit.y, player.VerticalLimit.x),
                    math.clamp(newPosition.z, player.HorizontalLimit.y, player.HorizontalLimit.x));
            }).ScheduleParallel();
        }
    }
}
