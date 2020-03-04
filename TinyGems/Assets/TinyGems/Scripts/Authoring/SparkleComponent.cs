using Unity.Entities;
using UnityEngine;

namespace Unity.TinyGems.Authoring
{
    public class SparkleComponent: MonoBehaviour, IConvertGameObjectToEntity
    {
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponent<SparkleObject>(entity);
        }
    }
}
