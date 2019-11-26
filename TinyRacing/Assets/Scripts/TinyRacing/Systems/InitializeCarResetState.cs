using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Reset the car state to default
    /// </summary>
    public class InitializeCarResetState : JobComponentSystem
    {
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            var ecb = ecbSystem.CreateCommandBuffer().ToConcurrent();
            var jobHandle = Entities.WithNone<CarDefaultState>().WithAll<Car>().ForEach(
                (Entity entity, int entityInQueryIndex, ref Translation translation, ref Rotation rotation) =>
                {
                    ecb.AddComponent(entityInQueryIndex, entity,
                        new CarDefaultState {StartPosition = translation.Value, StartRotation = rotation.Value});
                }).Schedule(inputDeps);
            ecbSystem.AddJobHandleForProducer(jobHandle);

            return jobHandle;
        }
    }
}