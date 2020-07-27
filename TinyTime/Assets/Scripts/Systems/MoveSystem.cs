using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TinyTime
{
    public class MoveSystem : SystemBase
    {
        protected override void OnStartRunning()
        {
            Entities.WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled)
                .ForEach((ref Move move, ref Translation translation) => { move.InitialPosition = translation.Value; })
                .ScheduleParallel();
            base.OnStartRunning();
        }

        protected override void OnUpdate()
        {
            var deltaTime = Time.DeltaTime;
            Entities.ForEach((ref Move move, ref Translation translation) =>
            {
                translation.Value = math.lerp(translation.Value, move.Destination, deltaTime * move.Speed);
                if (math.distance(translation.Value, move.Destination) < 0.1)
                {
                    if (move.Loop == LoopType.Loop)
                    {
                        translation.Value = move.InitialPosition;
                    }
                    else if (move.Loop == LoopType.PingPong)
                    {
                        var tmp = move.Destination;
                        move.Destination = move.InitialPosition;
                        move.InitialPosition = tmp;
                    }
                }
            }).ScheduleParallel();
        }
    }
}
