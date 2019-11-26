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

            var jobHandle = Entities.ForEach((Entity entity, int entityInQueryIndex, ref Smoke smoke,
                ref Translation translation, ref Scale scale,
                ref MeshRenderer renderer
                #if UNITY_DOTSPLAYER
                , ref LitMaterial litMaterial
                #endif
                ) =>
            {
                smoke.Timer += deltaTime;
                translation.Value += new float3(0f, smoke.Speed, 0f) * deltaTime;
                scale.Value = smoke.BaseScale * smoke.Timer / smoke.Duration;
#if UNITY_DOTSPLAYER
                litMaterial.transparent = true;
                var delta = smoke.Timer / smoke.Duration;
                // Inverse quadratic ease-in for smoke transparency
                litMaterial.constOpacity = 1 - delta * delta;
#endif
                if (smoke.Timer > smoke.Duration)
                    ecb.DestroyEntity(entityInQueryIndex, entity);
            }).Schedule(inputDeps);

            ecbSystem.AddJobHandleForProducer(jobHandle);

            return jobHandle;
        }
    }
}