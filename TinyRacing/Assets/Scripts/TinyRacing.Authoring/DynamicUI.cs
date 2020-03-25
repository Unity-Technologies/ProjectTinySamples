using Unity.Entities;
using Unity.Entities.Runtime.Build;
using Unity.Tiny.Rendering;
using UnityEngine;
using UnityEngine.Serialization;

namespace TinyRacing.Authoring
{

public class DynamicUI : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        if (!conversionSystem.TryGetBuildConfigurationComponent<DotsRuntimeBuildProfile>(out var _))
            return;
        Unity.Tiny.Rendering.MeshRenderer mr = dstManager.GetComponentData<Unity.Tiny.Rendering.MeshRenderer>(entity);
    }
}

}
