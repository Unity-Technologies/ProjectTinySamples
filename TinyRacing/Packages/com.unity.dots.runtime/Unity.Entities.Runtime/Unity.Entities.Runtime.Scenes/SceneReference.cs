using System;
using Unity.Entities;

namespace Unity.Entities.Runtime
{
    /// <summary>
    /// Container for scene guid that can be used to reference a scene with editor support.
    /// </summary>
    public struct SceneReference : IComponentData
    {
        public static readonly SceneReference Null = new SceneReference { SceneGuid = Guid.Empty };

        public Guid SceneGuid;
    }
}
