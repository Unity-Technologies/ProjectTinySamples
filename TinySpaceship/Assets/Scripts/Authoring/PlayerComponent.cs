using System.Collections.Generic;
using Unity.Spaceship;
using Unity.Entities;
using UnityEngine;

public class PlayerComponent : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
{
    public float RotationSpeed = 2f;
    public float MoveSpeed = 4f;
    public float FireRate = 3f;
    public float FireSpeed = 5f;
    public GameObject MissilePrefab = null;
    
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new Player
        {
            RotationSpeed = RotationSpeed,
            MoveSpeed = MoveSpeed,
            FireRate = FireRate,
            FireSpeed = FireSpeed,
            MissilePrefab = MissilePrefab != null ? conversionSystem.GetPrimaryEntity(MissilePrefab) : Entity.Null
        });
    }

    public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
    {
        if (MissilePrefab != null)
            referencedPrefabs.Add(MissilePrefab);
    }
}
