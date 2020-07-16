using System;
using Unity.Tiny.GenericAssetLoading;
using Unity.Entities;
using Unity.Tiny.Audio;
using System.Runtime.InteropServices;

namespace Unity.Tiny.Web
{
    // Unlike the native version, the HTML version manages the IDs for the clips and sources.
    static class IDPool
    {
        internal static int clipID;
        internal static int sourceID;
    }


    public struct AudioHTMLClip : ISystemStateComponentData
    {
        public int clipID;
    }

    public struct AudioHTMLLoading : ISystemStateComponentData
    {
    }

    struct AudioHTMLSource : IComponentData
    {
        public int sourceID;
    }

    static class AudioHTMLNativeCalls
    {
        private const string DLL = "lib_unity_tiny_audio_web";

        [DllImport(DLL, EntryPoint = "js_html_initAudio")]
        [return : MarshalAs(UnmanagedType.I1)]
        public static extern bool Init();

        [DllImport(DLL, EntryPoint = "js_html_audioIsUnlocked")]
        [return : MarshalAs(UnmanagedType.I1)]
        public static extern bool IsUnlocked();

        [DllImport(DLL, EntryPoint = "js_html_audioUnlock")]
        public static extern void Unlock();

        [DllImport(DLL, EntryPoint = "js_html_audioPause")]
        public static extern void Pause();

        [DllImport(DLL, EntryPoint = "js_html_audioResume")]
        public static extern void Resume();

        // Note this just returns the audioClipIndex, which isn't super helpful.
        [DllImport(DLL, EntryPoint = "js_html_audioStartLoadFile", CharSet = CharSet.Ansi)]
        public static extern int StartLoad([MarshalAs(UnmanagedType.LPStr)] string audioClipName, int audioClipIndex);

        // LoadResult:
        // stillWorking = 0,
        // success = 1,
        // failed = 2
        [DllImport(DLL, EntryPoint = "js_html_audioCheckLoad")]
        public static extern int CheckLoad(int audioClipIndex);

        [DllImport(DLL, EntryPoint = "js_html_audioFree")]
        public static extern void Free(int audioClipIndex);

        [DllImport(DLL, EntryPoint = "js_html_audioPlay")]
        [return : MarshalAs(UnmanagedType.I1)]
        public static extern bool Play(int audioClipIdx, int audioSourceIdx, double volume, double pitch, double pan, bool loop);

        [DllImport(DLL, EntryPoint = "js_html_audioStop")]
        [return : MarshalAs(UnmanagedType.I1)]
        public static extern bool Stop(int audioSourceIdx, bool doStop);

        [DllImport(DLL, EntryPoint = "js_html_audioSetVolume")]
        [return : MarshalAs(UnmanagedType.I1)]
        public static extern bool SetVolume(int audioSourceIdx, double volume);

        [DllImport(DLL, EntryPoint = "js_html_audioSetPan")]
        [return : MarshalAs(UnmanagedType.I1)]
        public static extern bool SetPan(int audioSourceIdx, double pan);

        [DllImport(DLL, EntryPoint = "js_html_audioSetPitch")]
        [return : MarshalAs(UnmanagedType.I1)]
        public static extern bool SetPitch(int audioSourceIdx, double pitch);

        [DllImport(DLL, EntryPoint = "js_html_audioIsPlaying")]
        [return : MarshalAs(UnmanagedType.I1)]
        public static extern bool IsPlaying(int audioSourceIdx);
    }

    class AudioHTMLSystemLoadFromFile : IGenericAssetLoader<AudioClip, AudioHTMLClip, AudioClipLoadFromFile, AudioHTMLLoading>
    {
        public void StartLoad(
            EntityManager entityManager,
            Entity e,
            ref AudioClip audioClip,
            ref AudioHTMLClip audioHtmlClip,
            ref AudioClipLoadFromFile loader,
            ref AudioHTMLLoading loading)
        {
            if (!entityManager.HasComponent<AudioClipLoadFromFileAudioFile>(e))
            {
                audioHtmlClip.clipID = 0;
                audioClip.status = AudioClipStatus.LoadError;
                return;
            }

            string path = entityManager.GetBufferAsString<AudioClipLoadFromFileAudioFile>(e);
            audioHtmlClip.clipID = ++IDPool.clipID;
            AudioHTMLNativeCalls.StartLoad(path, audioHtmlClip.clipID);
            audioClip.status = AudioClipStatus.Loading;
        }

        public LoadResult CheckLoading(IntPtr cppWrapper, EntityManager man, Entity e, ref AudioClip audioClip, ref AudioHTMLClip audioNative, ref AudioClipLoadFromFile param, ref AudioHTMLLoading loading)
        {
            LoadResult result = (LoadResult)AudioHTMLNativeCalls.CheckLoad(audioNative.clipID);

            if (result == LoadResult.success)
            {
                audioClip.status = AudioClipStatus.Loaded;
            }
            else if (result == LoadResult.failed)
            {
                audioClip.status = AudioClipStatus.LoadError;
            }

            return result;
        }

        public void FreeNative(EntityManager man, Entity e, ref AudioHTMLClip audioNative)
        {
            AudioHTMLNativeCalls.Free(audioNative.clipID);
        }

        public void FinishLoading(EntityManager man, Entity e, ref AudioClip audioClip, ref AudioHTMLClip audioNative, ref AudioHTMLLoading loading)
        {
        }
    }


    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(AudioHTMLSystem))]
    public class AudioIOHTMLSystem : GenericAssetLoader<AudioClip, AudioHTMLClip, AudioClipLoadFromFile, AudioHTMLLoading>
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            c = new AudioHTMLSystemLoadFromFile();
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public class AudioHTMLSystem : AudioSystem
    {
        private bool unlocked = false;
        private bool paused = false;

        protected override void OnUpdate()
        {
            base.OnUpdate();
            TinyEnvironment env = World.TinyEnvironment();
            AudioConfig ac = env.GetConfigData<AudioConfig>();
            if (ac.paused != paused)
            {
                paused = ac.paused;
                if (paused)
                    AudioHTMLNativeCalls.Pause();
                else
                    AudioHTMLNativeCalls.Resume();
            }
        }

        protected override void InitAudioSystem()
        {
            //Console.WriteLine("InitAudioSystem()");
            AudioHTMLNativeCalls.Init();
            unlocked = AudioHTMLNativeCalls.IsUnlocked();
            //Console.WriteLine("(re) checking un-locked: ");
            //Console.WriteLine(unlocked ? "true" : "false");

            TinyEnvironment env = World.TinyEnvironment();
            AudioConfig ac = env.GetConfigData<AudioConfig>();
            ac.initialized = true;
            ac.unlocked = unlocked;
            env.SetConfigData(ac);
        }

        protected override void DestroyAudioSystem()
        {
            // No-op in HTML
        }

        protected override bool PlaySource(Entity e)
        {
            var mgr = EntityManager;

            if (mgr.HasComponent<AudioSource>(e))
            {
                AudioSource audioSource = mgr.GetComponentData<AudioSource>(e);

                Entity clipEntity = audioSource.clip;
                if (mgr.HasComponent<AudioHTMLClip>(clipEntity))
                {
                    AudioHTMLClip clip = mgr.GetComponentData<AudioHTMLClip>(clipEntity);
                    if (clip.clipID > 0)
                    {
                        if (!unlocked)
                        {
                            AudioHTMLNativeCalls.Unlock();
                            unlocked = AudioHTMLNativeCalls.IsUnlocked();
                            if (unlocked)
                            {
                                TinyEnvironment env = World.TinyEnvironment();
                                AudioConfig ac = env.GetConfigData<AudioConfig>();
                                ac.unlocked = unlocked;
                                env.SetConfigData(ac);
                            }
                        }

                        if (unlocked)
                        {
                            // If there is an existing source, it should re-start.
                            // Do this with a Stop() and let it play below.
                            if (mgr.HasComponent<AudioHTMLSource>(e))
                            {
                                AudioHTMLSource ans = mgr.GetComponentData<AudioHTMLSource>(e);
                                AudioHTMLNativeCalls.Stop(ans.sourceID, true);
                            }

                            float volume = audioSource.volume;
                            float pan = mgr.HasComponent<Audio2dPanning>(e) ? mgr.GetComponentData<Audio2dPanning>(e).pan : 0.0f;
                            float pitch = mgr.HasComponent<AudioPitch>(e) ? mgr.GetComponentData<AudioPitch>(e).pitch : 1.0f;

                            // For 3d sounds, we start at volume zero because we don't know if this sound is close or far from the listener.
                            // It is much smoother to ramp up volume from zero than the alternative.
                            if (mgr.HasComponent<Audio3dPanning>(e))
                                volume = 0.0f;

                            int sourceID = ++IDPool.sourceID;

                            // Check the return value from Play because it fails sometimes at startup for AudioSources with PlayOnAwake set to true.
                            // If initial attempt fails, try again next frame.
                            if (!AudioHTMLNativeCalls.Play(clip.clipID, sourceID, volume, pitch, pan, audioSource.loop))
                                return false;

                            AudioHTMLSource audioNativeSource = new AudioHTMLSource()
                            {
                                sourceID = sourceID
                            };
                            // Need a native source as well.
                            if (mgr.HasComponent<AudioHTMLSource>(e))
                            {
                                mgr.SetComponentData(e, audioNativeSource);
                            }
                            else
                            {
                                mgr.AddComponentData(e, audioNativeSource);
                            }

                            return true;
                        }
                    }
                }
            }
            return false;
        }

        protected override void StopSource(Entity e)
        {
            if (EntityManager.HasComponent<AudioHTMLSource>(e))
            {
                AudioHTMLSource audioNativeSource = EntityManager.GetComponentData<AudioHTMLSource>(e);
                if (audioNativeSource.sourceID > 0)
                {
                    AudioHTMLNativeCalls.Stop(audioNativeSource.sourceID, true);
                }
            }
        }

        protected override bool IsPlaying(Entity e)
        {
            if (EntityManager.HasComponent<AudioHTMLSource>(e))
            {
                AudioHTMLSource audioHtmlSource = EntityManager.GetComponentData<AudioHTMLSource>(e);
                if (audioHtmlSource.sourceID > 0)
                {
                    return AudioHTMLNativeCalls.IsPlaying(audioHtmlSource.sourceID);
                }
            }
            return false;
        }

        protected override bool SetVolume(Entity e, float volume)
        {
            if (EntityManager.HasComponent<AudioHTMLSource>(e))
            {
                AudioHTMLSource audioNativeSource = EntityManager.GetComponentData<AudioHTMLSource>(e);
                if (audioNativeSource.sourceID > 0)
                {
                    return AudioHTMLNativeCalls.SetVolume(audioNativeSource.sourceID, volume);
                }
            }
            return false;
        }

        protected override bool SetPan(Entity e, float pan)
        {
            if (EntityManager.HasComponent<AudioHTMLSource>(e))
            {
                AudioHTMLSource audioNativeSource = EntityManager.GetComponentData<AudioHTMLSource>(e);
                if (audioNativeSource.sourceID > 0)
                {
                    return AudioHTMLNativeCalls.SetPan(audioNativeSource.sourceID, pan);
                }
            }
            return false;
        }

        protected override bool SetPitch(Entity e, float pitch)
        {
            if (EntityManager.HasComponent<AudioHTMLSource>(e))
            {
                AudioHTMLSource audioNativeSource = EntityManager.GetComponentData<AudioHTMLSource>(e);
                if (audioNativeSource.sourceID > 0)
                {
                    return AudioHTMLNativeCalls.SetPitch(audioNativeSource.sourceID, pitch);
                }
            }
            return false;
        }
    }
}
