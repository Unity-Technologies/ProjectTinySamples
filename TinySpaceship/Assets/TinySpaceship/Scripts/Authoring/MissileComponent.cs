using Unity.Entities;
using UnityEngine;

namespace Unity.Spaceship.Authoring
{
    public class MissileComponent : MonoBehaviour, IConvertGameObjectToEntity
    {
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new Missile {});
        }
    }
}