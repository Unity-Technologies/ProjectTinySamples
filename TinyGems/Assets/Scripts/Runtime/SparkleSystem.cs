using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Tiny;

namespace Unity.TinyGems
{
    public class SparkleSystem : SystemBase
    {
        private const float k_SparkleDuration = 0.7f;
        private const float k_DelayMin = 3f;
        private const float k_DelayMax = 7f;

        private uint m_LastSeed = 1234;

        protected override void OnUpdate()
        {
            var ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            var cmdBuffer = ecbSystem.CreateCommandBuffer().AsParallelWriter();

            var random = new Random(m_LastSeed);
            m_LastSeed = random.NextUInt();
            
            var currentTime = Time.ElapsedTime;
            Dependency = Entities
                .ForEach((
                    Entity entity,
                    int entityInQueryIndex,
                    ref SpriteRenderer spriteRenderer,
                    ref NonUniformScale nonUniformScale,
                    ref SparkleObject sparkleObject,
                    ref Rotation rotation) =>
                {
                    if (sparkleObject.StartTime == 0f)
                    {
                        sparkleObject.StartTime = random.NextFloat(0.5f, 3.5f);
                        sparkleObject.Delay = random.NextFloat(k_DelayMin, k_DelayMax);
                        cmdBuffer.SetComponent(entityInQueryIndex, entity, sparkleObject);
                    }
                    else if ((currentTime - sparkleObject.StartTime) < k_SparkleDuration)
                    {
                        var time = currentTime - sparkleObject.StartTime;
                        var currentValue = math.sin(time * (2.0f * math.PI) * k_SparkleDuration);
                        
                        nonUniformScale.Value = (float)currentValue;
                        cmdBuffer.SetComponent(entityInQueryIndex, entity, nonUniformScale);

                        spriteRenderer.Color.w = (float)currentValue;
                        cmdBuffer.SetComponent(entityInQueryIndex, entity, spriteRenderer);

                        sparkleObject.Delay = random.NextFloat(k_DelayMin, k_DelayMax);
                        cmdBuffer.SetComponent(entityInQueryIndex, entity, sparkleObject);
                    }
                    else if ((currentTime - (sparkleObject.StartTime) + k_SparkleDuration) > sparkleObject.Delay)
                    {
                        nonUniformScale.Value = 0f;
                        cmdBuffer.SetComponent(entityInQueryIndex, entity, nonUniformScale);

                        spriteRenderer.Color.w = 0f;
                        cmdBuffer.SetComponent(entityInQueryIndex, entity, spriteRenderer);
                        
                        rotation.Value = quaternion.RotateZ(random.NextFloat(0f, 180f));
                        cmdBuffer.SetComponent(entityInQueryIndex, entity, rotation);
                        
                        sparkleObject.StartTime = (float)currentTime;
                        cmdBuffer.SetComponent(entityInQueryIndex, entity, sparkleObject);
                    }
                }).Schedule(Dependency);

            ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}