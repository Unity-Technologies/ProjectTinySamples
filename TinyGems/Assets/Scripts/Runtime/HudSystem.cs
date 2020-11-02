using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace Unity.TinyGems
{
    [UpdateAfter(typeof(TransformSystemGroup))]
    public class HudSystem : SystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<ActiveScene>();
        }

        protected override void OnUpdate()
        {
            var activeScene = GetSingleton<ActiveScene>();
            var cmdBuffer = new EntityCommandBuffer(Allocator.TempJob);

            Entities
                .ForEach((
                    in Entity entity,
                    in HudShowState showScene,
                    in DynamicBuffer<HudObject> hudObjects) =>
                {
                    for (var i = 0; i < hudObjects.Length; i++)
                    {
                        var isVisible = activeScene.Value == showScene.Value;

                        if (isVisible)
                            cmdBuffer.RemoveComponent<Disabled>(hudObjects[i].Value);
                        else
                            cmdBuffer.AddComponent<Disabled>(hudObjects[i].Value);
                    }
                }).Run();
            
            cmdBuffer.Playback(EntityManager);
            cmdBuffer.Dispose();
        }
    }
}
