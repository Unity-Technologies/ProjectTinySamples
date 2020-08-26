using Unity.Spaceship;
using Unity.Entities;
using UnityEngine;

public class AsteroidComponent : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponent<Asteroid>(entity);
    }
}
