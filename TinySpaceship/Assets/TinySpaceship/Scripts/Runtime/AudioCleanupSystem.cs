using Unity.Entities;
using Unity.Jobs;
using Unity.Tiny.Audio;

namespace Unity.Spaceship
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public class AudioCleanupSystem : JobComponentSystem 
    {
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            var cmdBuffer = ecbSystem.CreateCommandBuffer().ToConcurrent();
            
            var sourceStopTags = GetComponentDataFromEntity<AudioSourceStop>();
            inputDeps = Entities
                .WithReadOnly(sourceStopTags)
                .WithName("CleanupAudioJob")
                .ForEach((Entity entity, int entityInQueryIndex, AudioSource audioSource) =>
                {
                    if (audioSource.isPlaying)
                        return;

                    if ((audioSource.loop && sourceStopTags.HasComponent(entity)) ||
                        !audioSource.loop)
                        cmdBuffer.DestroyEntity(entityInQueryIndex, entity);

                }).Schedule(inputDeps);
            
            ecbSystem.AddJobHandleForProducer(inputDeps);
            
            return inputDeps;
        }
    }
}
