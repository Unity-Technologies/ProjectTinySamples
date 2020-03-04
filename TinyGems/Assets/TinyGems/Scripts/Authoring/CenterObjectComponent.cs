using UnityEngine;
using Unity.Entities;

namespace Unity.TinyGems.Authoring
{
    public class CenterObjectComponent : MonoBehaviour, IConvertGameObjectToEntity
    {
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new CenterObject());
        }
    }
}