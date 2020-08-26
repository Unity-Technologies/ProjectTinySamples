using Unity.Entities;
using UnityEngine;

namespace Unity.Spaceship.Authoring
{
    public class HudComponent : MonoBehaviour, IConvertGameObjectToEntity
    {
        public GameStates ShowingState = GameStates.None;
        public GameObject[] HudObjects = null;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            if (HudObjects == null)
                return;
            
            dstManager.AddComponentData(entity, new HudShowState
            {
                Value = ShowingState,
            });
            
            var hudObjects = dstManager.AddBuffer<HudObject>(entity);
            for(var i = 0; i < HudObjects.Length; i++)
            {
                hudObjects.Add(new HudObject()
                { 
                    Value = HudObjects[i] != null ? conversionSystem.GetPrimaryEntity(HudObjects[i]) : Entity.Null
                });
            }
        }
    }
}