using System.Collections.Generic;
using Unity.Build;
using Unity.Properties;
using BuildTarget = Unity.Platforms.BuildTarget;

namespace Unity.Entities.Runtime.Build
{
    public sealed class DotsRuntimeBuildProfile : IBuildPipelineComponent
    {
        BuildTarget m_Target;
        List<string> m_ExcludedAssemblies;

        /// <summary>
        /// Retrieve <see cref="BuildTypeCache"/> for this build profile.
        /// </summary>
        public BuildTypeCache TypeCache { get; } = new BuildTypeCache();

        /// <summary>
        /// Gets or sets which <see cref="Platforms.BuildTarget"/> this profile is going to use for the build.
        /// Used for building Dots Runtime players.
        /// </summary>
        [CreateProperty]
        public BuildTarget Target
        {
            get => m_Target;
            set
            {
                m_Target = value;
                TypeCache.PlatformName = m_Target?.UnityPlatformName;
            }
        }

        public int SortingIndex => 0;
        public bool SetupEnvironment() => false;

        /// <summary>
        /// Gets or sets which <see cref="Configuration"/> this profile is going to use for the build.
        /// </summary>
        [CreateProperty]
        public BuildType Configuration { get; set; } = BuildType.Develop;

        public BuildPipelineBase Pipeline { get; set; } = new DotsRuntimeBuildPipeline();

        /// <summary>
        /// List of assemblies that should be explicitly excluded for the build.
        /// </summary>
        //[CreateProperty]
        public List<string> ExcludedAssemblies
        {
            get => m_ExcludedAssemblies;
            set
            {
                m_ExcludedAssemblies = value;
                TypeCache.ExcludedAssemblies = value;
            }
        }

        public DotsRuntimeBuildProfile()
        {
            Target = BuildTarget.DefaultBuildTarget;
            ExcludedAssemblies = new List<string>();
        }
    }
}
