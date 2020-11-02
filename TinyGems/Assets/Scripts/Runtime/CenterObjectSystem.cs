using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.TinyGems
{
    [UpdateAfter(typeof(HudSystem))]
    public class CenterObjectSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireSingletonForUpdate<CellManager>();
        }

        protected override void OnUpdate()
        {
            var cmdBuffer = new EntityCommandBuffer(Allocator.TempJob);
            var cellMan = GetSingleton<CellManager>();
            Entities
                .WithAll<CenterObjectTag>()
                .ForEach((
                    ref Translation translation,
                    in Entity entity) =>
            {
                var x = cellMan.MaxCol * 0.5f;
                var y = cellMan.MaxRow * 0.5f;
                translation.Value = new float3(x, y, translation.Value.z);
                
                cmdBuffer.RemoveComponent<CenterObjectTag>(entity);
            }).Run();
            
            cmdBuffer.Playback(EntityManager);
            cmdBuffer.Dispose();
        }
    }
}