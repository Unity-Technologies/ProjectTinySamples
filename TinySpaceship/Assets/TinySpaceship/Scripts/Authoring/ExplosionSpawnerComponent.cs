using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Unity.Spaceship.Authoring
{
    public class ExplosionSpawnerComponent : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
    {
        public Sprite[] Sprites;
        
        public GameObject Prefab = null;
        public float TimePerSprite = 0.05f;
        
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new ExplosionSpawner
            {
                Prefab = conversionSystem.GetPrimaryEntity(Prefab),
                TimePerSprite = TimePerSprite,
            });
            
            var buffer = dstManager.AddBuffer<ExplosionSprite>(entity);

            if (Sprites == null)
                return;
            
            foreach (var s in Sprites)
            {
                buffer.Add(new ExplosionSprite
                {
                    Sprite = conversionSystem.GetPrimaryEntity(s)
                });
            }
        }

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            if(Prefab != null)
                referencedPrefabs.Add(Prefab);
        }
    }
    
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))] 
    class DeclareExplosionSpriteReference : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ExplosionSpawnerComponent mgr) =>
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