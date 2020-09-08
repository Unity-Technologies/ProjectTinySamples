using Unity.Entities;
using Unity.Tiny.Audio;

namespace Unity.Spaceship
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public class AudioCleanupSystem : SystemBase 
    {
        protected override void OnUpdate()
        {
            var ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            var cmdBuffer = ecbSystem.CreateCommandBuffer().AsParallelWriter();
            
            var sourceStopTags = GetComponentDataFromEntity<AudioSourceStop>();
            Dependency = Entities
                .WithReadOnly(sourceStopTags)
                .WithName("CleanupAudioJob")
                .ForEach((
                    int entityInQueryIndex,
                    in Entity entity, 
                    in AudioSource audioSource) =>
                {
                    if (audioSource.isPlaying)
                        return;

                    if ((audioSource.loop && sourceStopTags.HasComponent(entity)) ||
                        !audioSource.loop)
                        cmdBuffer.DestroyEntity(entityInQueryIndex, entity);

                }).Schedule(Dependency);
            
            ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}