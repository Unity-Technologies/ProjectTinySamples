using Unity.Entities;
using UnityEngine;
using Unity.Tiny.Rendering;

namespace TinyRacing.Authoring
{

[DisallowMultipleComponent]
public class UIMaterialsAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public Material[] Numbers;
    public Material Empty;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponent<UINumbers>(entity);
        dstManager.AddBuffer<UINumberMaterial>(entity);

        //Add additional entity for the Empty material
        var primaryEntity = conversionSystem.GetPrimaryEntity(Empty);
        var mat = dstManager.GetComponentData<SimpleMaterial>(primaryEntity);
        Entity additionalEntity = conversionSystem.CreateAdditionalEntity(Empty);
        dstManager.AddComponent<DynamicMaterial>(additionalEntity);
        dstManager.AddComponentData<SimpleMaterial>(additionalEntity, mat);

        //Add additional entities for each numbers material
        for (int i = 0; i < Numbers.Length; i++)
        {
            primaryEntity = conversionSystem.GetPrimaryEntity(Numbers[i]);
            mat = dstManager.GetComponentData<SimpleMaterial>(primaryEntity);
            additionalEntity = conversionSystem.CreateAdditionalEntity(Numbers[i]);
            dstManager.AddComponent<DynamicMaterial>(additionalEntity);
            dstManager.AddComponentData<SimpleMaterial>(additionalEntity, mat);
        }
    }
}

[UpdateInGroup(typeof(GameObjectAfterConversionGroup))]
internal class AddUIMaterialsReference : GameObjectConversionSystem
{
    protected override void OnUpdate()
    {
        Entities.ForEach((UIMaterialsAuthoring uNum) =>
        {
            var primaryEntity = GetPrimaryEntity(uNum);
            var buffer = DstEntityManager.GetBuffer<UINumberMaterial>(primaryEntity);

            //Add empty material first in the buffer, take the second additional entity
            var entities = GetEntities(uNum.Empty);
            if (entities.MoveNext() && entities.MoveNext())
                buffer.Add(new UINumberMaterial() { MaterialEntity = entities.Current });

            //Add number materials and take the second additional entity
            for (int i = 0; i < uNum.Numbers.Length; i++)
            {
                entities = GetEntities(uNum.Numbers[i]);
                if(entities.MoveNext() && entities.MoveNext())
                    buffer.Add(new UINumberMaterial { MaterialEntity = entities.Current });
            }
        });
    }
}

[UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
internal class DeclareNumberMaterials : GameObjectConversionSystem
{
    protected override void OnUpdate()
    {
        Entities.ForEach((UIMaterialsAuthoring uNum) =>
        {
            if (!uNum.Empty)
            {
                Debug.LogWarning("Missing Empty material on UI Material authoring component");
                return;
            }
            if (uNum.Numbers.Length == 0)
            {
                Debug.LogWarning("Missing Numbers material on UI Material authoring component");
                return;
            }
            DeclareReferencedAsset(uNum.Empty);
            foreach (Material mat in uNum.Numbers)
                DeclareReferencedAsset(mat);
        });
    }
}

}
