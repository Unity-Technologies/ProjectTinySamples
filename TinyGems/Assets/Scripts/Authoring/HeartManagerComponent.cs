using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

namespace Unity.TinyGems.Authoring
{
    [ConverterVersion("TinyGems", 1)]
    public class HeartManagerComponent : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
    {
        public GameObject HeartPrefab;
        public GameObject HeartBackgroundPrefab;
        public int NoOfHearts;
        
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new HeartManager()
            {
                HeartPrefab = conversionSystem.GetPrimaryEntity(HeartPrefab),
                HeartBackgroundPrefab = conversionSystem.GetPrimaryEntity(HeartBackgroundPrefab),
                NoOfHearts = NoOfHearts
            });
        }

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            referencedPrefabs.Add(HeartPrefab);
            referencedPrefabs.Add(HeartBackgroundPrefab);
        }
    }
}
