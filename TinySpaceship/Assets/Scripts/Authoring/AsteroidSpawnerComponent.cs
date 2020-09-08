using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Unity.Spaceship.Authoring
{
    public class AsteroidSpawnerComponent : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
    {
        public Sprite[] Sprites;
        
        public GameObject Prefab = null;
        public float SpawnRate = 1f;
        public float MinSpeed = 0.5f;
        public float MaxSpeed = 3f;
        public float PathVariation = 0.1f;
        
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new AsteroidSpawner
            {
                Prefab = conversionSystem.GetPrimaryEntity(Prefab),
                Rate = SpawnRate,
                MinSpeed = MinSpeed,
                MaxSpeed = MaxSpeed,
                PathVariation = PathVariation
            });

            dstManager.AddComponentData(entity, new GameState
            {
                Value = GameStates.Start
            });

            var buffer = dstManager.AddBuffer<AsteroidSprite>(entity);

            if (Sprites == null)
                return;
            
            foreach (var s in Sprites)
            {
                buffer.Add(new AsteroidSprite
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
    class DeclareAsteroidSpriteReference : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((AsteroidSpawnerComponent mgr) =>
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