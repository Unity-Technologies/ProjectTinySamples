using Unity.Entities;
using Unity.Tiny.Rendering;
using UnityEngine;
using UnityEngine.Serialization;

namespace TinyRacing.Authoring
{

public class DynamicUI : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        Unity.Tiny.Rendering.MeshRenderer mr = dstManager.GetComponentData<Unity.Tiny.Rendering.MeshRenderer>(entity);
        dstManager.AddComponent<DynamicMaterial>(mr.material);
    }
}

}
