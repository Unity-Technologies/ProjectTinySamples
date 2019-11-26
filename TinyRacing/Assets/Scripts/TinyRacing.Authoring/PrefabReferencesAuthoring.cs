using TinyRacing.Systems.Hybrid;
using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;
using TinyRacing;

[DisallowMultipleComponent]
public class PrefabReferencesAuthoring : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
{
    public GameObject CarSmokePrefabMeshRenderer;
    public GameObject CarSmokeDestroyedPrefabMeshRenderer;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new PrefabReferences()
        {
            carSmokePrefab = conversionSystem.GetPrimaryEntity(CarSmokePrefabMeshRenderer),
            carSmokeDestroyedPrefab = conversionSystem.GetPrimaryEntity(CarSmokeDestroyedPrefabMeshRenderer)
        });
    }

    public void DeclareReferencedPrefabs(List<GameObject> gameObjects)
    {
        gameObjects.Add(CarSmokePrefabMeshRenderer);
        gameObjects.Add(CarSmokeDestroyedPrefabMeshRenderer);
    }
}
