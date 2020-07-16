using System.IO;
using Unity.Build;
using Unity.Properties;

namespace Unity.Entities.Runtime.Build
{
    internal sealed class DotsRuntimeBuildArtifact : IBuildArtifact
    {
        [CreateProperty] public FileInfo OutputTargetFile { get; set; }
    }
}
