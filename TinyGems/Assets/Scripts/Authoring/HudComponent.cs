using Unity.Entities;
using UnityEngine;

namespace Unity.TinyGems.Authoring
{
    [ConverterVersion("TinyGems", 1)]
    public class HudComponent : MonoBehaviour, IConvertGameObjectToEntity
    {
        public Scenes ShowingScene = Scenes.None;
        public GameObject[] HudObjects = null;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            if (HudObjects == null)
                return;
            
            dstManager.AddComponentData(entity, new HudShowState
            {
                Value = ShowingScene,
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