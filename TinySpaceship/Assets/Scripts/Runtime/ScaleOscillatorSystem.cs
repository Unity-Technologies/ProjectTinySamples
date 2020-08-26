using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.Spaceship
{
    public class ScaleOscillatorSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var time = (float)Time.ElapsedTime;
            Dependency = Entities.ForEach((Entity e, ref ScaleOscillator scaleOscillator, ref NonUniformScale scale) =>
            {
                if (math.abs(scaleOscillator.BaseScale.x - float.MaxValue) < 0.01f)
                {
                    scaleOscillator.BaseScale = scale.Value;
                }
                scale.Value = scaleOscillator.BaseScale + math.sin(time * (2.0f * math.PI) * scaleOscillator.PulseSpeed) * scaleOscillator.PulseSize;
            }).Schedule(Dependency);
        }
    }
}