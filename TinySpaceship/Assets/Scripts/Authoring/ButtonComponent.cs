using UnityEngine;
using Unity.Entities;

namespace Unity.Spaceship.Authoring
{
    public class ButtonComponent : MonoBehaviour, IConvertGameObjectToEntity
    {
        public ButtonTypes ButtonType = ButtonTypes.None;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new ButtonIdentifier()
            {
                Value = ButtonType
            });
        }
    }
}
