using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Tiny.Audio;

namespace Unity.Spaceship
{
    public static class AudioUtils
    {
        public static void PlaySound(EntityManager entityManager, AudioTypes audioType, bool shouldLoop = false)
        {
            var clipEntity = FindAudioClip(entityManager, audioType);
            if (clipEntity == Entity.Null)
                return;

            var sourceEntity = entityManager.CreateEntity();

            entityManager.AddComponentData(sourceEntity, new AudioSource()
            {
                clip = clipEntity,
                loop = shouldLoop,
                volume = 1f
            });
            
            entityManager.AddComponent<AudioSourceStart>(sourceEntity);
        }

        public static void StopSound(EntityManager entityManager, AudioTypes audioType)
        {
            var clipEntity = FindAudioClip(entityManager, audioType);
            if (clipEntity == Entity.Null)
                return;

            var audioSourceQuery = entityManager.CreateEntityQuery(typeof(AudioSource));

            var audioSources = audioSourceQuery.ToComponentDataArray<AudioSource>(Allocator.TempJob);
            var audioSourceEntities = audioSourceQuery.ToEntityArray(Allocator.TempJob);

            for (var i = 0; i < audioSources.Length; i++)
            {
                if (audioSources[i].clip != clipEntity)
                    continue;

                entityManager.AddComponent<AudioSourceStop>(audioSourceEntities[i]);
            }

            audioSources.Dispose();
            audioSourceEntities.Dispose();
        }
        
        private static Entity GetAudioLibrary(EntityManager entityManager)
        {
            var libraryQuery = entityManager.CreateEntityQuery(typeof(AudioLibrary));
            return libraryQuery.GetSingletonEntity();
        }        

        private static Entity FindAudioClip(EntityManager entityManager, AudioTypes audioType)
        {
            var library = GetAudioLibrary(entityManager);
            var audioClips = entityManager.GetBuffer<AudioObject>(library);
            
            return FindAudioClip(audioClips, audioType);
        }

        private static Entity FindAudioClip(DynamicBuffer<AudioObject> audioClips, AudioTypes audioType)
        {
            var clipIndex = int.MaxValue;
            for (var i = 0; i < audioClips.Length; i++)
            {
                if (audioClips[i].Type == audioType)
                {
                    clipIndex = i;
                    break;
                }
            }

            return clipIndex == int.MaxValue ? Entity.Null : audioClips[clipIndex].Clip;            
        }
    }
}
