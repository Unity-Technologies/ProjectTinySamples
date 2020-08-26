using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

namespace Unity.TinyGems.Authoring
{
    [ConverterVersion("TinyGems", 1)]
    public class CellManagerComponent : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
    {
        public Sprite[] Sprites;
        public GameObject CellPrefab;
        public GameObject CellBackground;
        public int MaxCol;
        public int MaxRow;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            if(CellPrefab == null)
                return;

            dstManager.AddComponentData(entity, new CellManager 
            {
                CellPrefab = conversionSystem.GetPrimaryEntity(CellPrefab),
                CellBackground = conversionSystem.GetPrimaryEntity(CellBackground),
                MaxCol = MaxCol,
                MaxRow = MaxRow
            });

            var buffer = dstManager.AddBuffer<CellSprite>(entity);
            foreach(var s in Sprites)
            {
                buffer.Add(new CellSprite{
                    Sprite = conversionSystem.GetPrimaryEntity(s)
                });
            }
        }

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            if(CellPrefab != null)
                referencedPrefabs.Add(CellPrefab);
            
            if(CellBackground != null)
                referencedPrefabs.Add(CellBackground);
        }
    }

    [ConverterVersion("TinyGems", 1)]
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))] 
    public class DeclareCellSpriteReference : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((CellManagerComponent mgr) =>
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