using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.Transforms;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Update smoke car exhaust smoke particles to animate them and destroy them after a moment.
    /// </summary>
    [UpdateAfter(typeof(SpawnSmoke))]
    public class UpdateSmoke : JobComponentSystem
    {
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            var deltaTime = Time.DeltaTime;
            var ecb = ecbSystem.CreateCommandBuffer().ToConcurrent();

            var jobHandle = Entities.ForEach((Entity entity, int nativeThreadIndex,
                ref Smoke smoke, ref Translation translation, ref Scale scale) =>
            {
                smoke.Timer += deltaTime;
                if (smoke.Timer > smoke.Duration)
                {
                    ecb.DestroyEntity(nativeThreadIndex, entity);
                    return;
                }

                translation.Value += new float3(0f, smoke.Speed, 0f) * deltaTime;
                scale.Value = smoke.BaseScale * smoke.Timer / smoke.Duration;
            }).Schedule(inputDeps);

            ecbSystem.AddJobHandleForProducer(jobHandle);

            return jobHandle;
        }
    }
}