using Unity.Entities;
using UnityEngine;

namespace Unity.Spaceship.Authoring
{
    public class ExplosionComponent : MonoBehaviour, IConvertGameObjectToEntity
    {
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponent<Explosion>(entity);
        }
    }
}