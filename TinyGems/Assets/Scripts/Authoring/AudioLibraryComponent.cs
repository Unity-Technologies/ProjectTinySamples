using UnityEngine;
using Unity.Entities;

namespace Unity.TinyGems.Authoring
{
    [ConverterVersion("TinyGems", 1)]
    public class AudioLibraryComponent : MonoBehaviour, IConvertGameObjectToEntity
    {
        [System.Serializable]
        public class AudioData
        {
            public AudioTypes Type;
            public AudioClip Clip;
        }
        
        public AudioData[] Data;
        
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new AudioLibrary() { });

            var audioObjects = dstManager.AddBuffer<AudioObject>(entity);
            for(var i = 0; i < Data.Length; i++)
            {
                audioObjects.Add(new AudioObject()
                {
                    Type = Data[i] != null ? Data[i].Type : AudioTypes.None,
                    Clip = Data[i] != null ? conversionSystem.GetPrimaryEntity(Data[i].Clip) : Entity.Null
                });
            }            
        }
    }
    
    [ConverterVersion("TinyGems", 1)]
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))] 
    public class DeclareAudioClipReference : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((AudioLibraryComponent libraryComponent) =>
            {
                if (libraryComponent.Data == null) 
                { return; }
                
                foreach (var data in libraryComponent.Data)
                {
                    if (data == null || data.Clip == null)
                    { continue; }
                    
                    DeclareReferencedAsset(data.Clip);
                }
            });
        }
    }  
}
