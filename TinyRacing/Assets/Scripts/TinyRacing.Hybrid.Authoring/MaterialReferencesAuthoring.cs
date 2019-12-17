using TinyRacing.Systems.Hybrid;
using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public class MaterialReferencesAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public Material Empty;
    public Material[] Numbers;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddSharedComponentData(entity, new MaterialReferences()
        {
            Empty = Empty,
            Numbers = Numbers
        });
    }
}