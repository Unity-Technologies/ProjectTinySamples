using TinyRacing.Systems.Hybrid;
using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public class MaterialReferencesAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public Material Empty;
    public Material[] Numbers;
    public Mesh CarSmokeMesh;
    public Material CarSmokeMaterial;
    public Material CarDestroyedSmokeMaterial;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddSharedComponentData(entity, new MaterialReferences()
        {
            Empty = Empty,
            Numbers = Numbers,
            CarSmokeMesh = CarSmokeMesh,
            CarSmokeMaterial = CarSmokeMaterial,
            CarDestroyedSmokeMaterial = CarDestroyedSmokeMaterial
        });
    }
}