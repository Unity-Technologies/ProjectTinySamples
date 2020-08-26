using Unity.Entities;
using UnityEngine;

namespace Unity.Spaceship.Authoring
{
    public class SpriteSelectionComponent : MonoBehaviour, IConvertGameObjectToEntity
    {
        public Sprite[] Sprites = null;
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            if (Sprites == null)
                return;
            
            var sprites = dstManager.AddBuffer<SpriteSelection>(entity);
            for(var i = 0; i < Sprites.Length; i++)
            {
                sprites.Add(new SpriteSelection()
                { 
                    Value = Sprites[i] != null ? conversionSystem.GetPrimaryEntity(Sprites[i]) : Entity.Null
                });
            }

            dstManager.AddComponentData(entity, new SpriteSelectionNotSetup());
        }
    }
    
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))] 
    public class DeclareSpriteSelectionReference : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((SpriteSelectionComponent mgr) =>
            {
                if (mgr.Sprites == null)
                    return;
                
                foreach (var s in mgr.Sprites)
                {
                    DeclareReferencedAsset(s);
                }
            });
        }
    }  
}