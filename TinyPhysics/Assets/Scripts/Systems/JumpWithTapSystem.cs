using Unity.Entities;

namespace TinyPhysics.Systems
{
    /// <summary>
    ///     Detect when an object is tapped and set its jump trigger
    /// </summary>
    [UpdateBefore(typeof(MovementSystem))]
    public class JumpWithTapSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.WithAll<JumpWithTap>().ForEach((ref Tappable tappable, ref Jumper jumper) =>
            {
                if (tappable.IsTapped)
                {
                    // Set jump trigger
                    jumper.JumpTrigger = true;

                    // Consume tap
                    tappable.IsTapped = false;
                }
            }).ScheduleParallel();
        }
    }
}
