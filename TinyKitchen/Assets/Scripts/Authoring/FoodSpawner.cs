using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace TinyKitchen
{
    public class FoodSpawner : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
    {
        public List<GameObject> foodPrefabs;
        
        // TODO spawn position

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new FoodSpawnerComponent()
            {
            });
            
            // TODO temporarily placing this here
            dstManager.AddComponentData(entity, new Game
            {
                gameState = GameState.Initialization,
                score =  0,
                currentLevel = 0
            });
            
            var buffer = dstManager.AddBuffer<FoodComponent>(entity);

            foreach (var foodPrefab in foodPrefabs)
            {
                buffer.Add(new FoodComponent
                {
                    Food = conversionSystem.GetPrimaryEntity(foodPrefab)
                });
            }
        }

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            referencedPrefabs.AddRange(foodPrefabs);
        }
    }
}