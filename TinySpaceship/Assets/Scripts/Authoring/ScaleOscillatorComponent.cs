using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

namespace Unity.Spaceship.Authoring
{
    public class ScaleOscillatorComponent : MonoBehaviour, IConvertGameObjectToEntity
    {
        public float PulseSpeed = 1.5f;
        public float PulseSize = 0.05f;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new ScaleOscillator
            {
                PulseSpeed = PulseSpeed,
                PulseSize = PulseSize,
                BaseScale = new float3(float.MaxValue)
            });
        }
    }
}