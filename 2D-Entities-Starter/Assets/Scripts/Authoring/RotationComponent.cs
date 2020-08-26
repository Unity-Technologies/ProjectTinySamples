using UnityEngine;
using Unity.Entities;

namespace Tiny2D
{
    [ConverterVersion("Tiny2D", 1)]
    public class RotationComponent : MonoBehaviour, IConvertGameObjectToEntity
    {
        public float RotationSpeed = 1f;
        
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new RotationSpeed()
            {
                Value = RotationSpeed
            });
        }
    }
}