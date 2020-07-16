using Unity.Entities;

namespace Unity.Entities.Runtime
{
    /// <summary>
    /// Stores a list of scenes that will be automatically loaded at boot time.
    /// </summary>
    //[HideInInspector]
    public struct StartupScenes : IBufferElementData
    {
        public SceneReference SceneReference;
    }
}
