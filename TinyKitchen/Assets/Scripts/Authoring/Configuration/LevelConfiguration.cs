using UnityEngine;
using Unity.Entities;

namespace TinyKitchen
{
    public class LevelConfiguration : MonoBehaviour, IConvertGameObjectToEntity
    {
        public LevelConfigurationData[] levelConfigurationData;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            DynamicBuffer<LevelBufferElement> levelBuffer = dstManager.AddBuffer<LevelBufferElement>(entity);
            foreach (var levelData in levelConfigurationData)
            {
                levelBuffer.Add(new LevelBufferElement
                {
                    potPosition = levelData.potPosition,
                    radius = levelData.radius,
                    isMoving = levelData.isMoving,
                    potSpeed = levelData.potSpeed,
                    initialPosition = levelData.initialPosition,
                    finalPosition = levelData.finalPosition,

                    fanPosition = levelData.fanPosition,
                    fanHeading = levelData.fanHeading,
                    fanForce = levelData.fanForce,
                    isRotating = levelData.isRotating,
                    rotationSpeed = levelData.rotationSpeed,
                    initialHeading = levelData.initialHeading,
                    finalHeading = levelData.finalHeading,
                    fanUIPos = levelData.fanUIPos,
                });

                //TODO: Nested DynamicBuffers. I don't think this will work, so we need to think about the structure to allow multiple obstacles per levelData. But since we don't have obstacles yet, I will comment this out.
                //DynamicBuffer<ObstacleBufferElement> obstacleBuffer = dstManager.AddBuffer<ObstacleBufferElement>(entity);
                //foreach (var obstacle in levelData.obstacles)
                //{
                //    obstacleBuffer.Add(new ObstacleBufferElement
                //    {
                //        Entity = conversionSystem.GetPrimaryEntity(obstacle.prefab),
                //        Position = obstacle.position,
                //        Scale = obstacle.scale
                //    });
                //}
            }
        }
    }
}
