using System;
using System.IO;
using Unity.Build;
using Unity.Build.Common;

namespace Unity.Entities.Runtime.Build
{
    [BuildStep(Description = "Running Bee")]
    sealed class BuildStepRunBee : BuildStepBase
    {
        public override Type[] UsedComponents { get; } =
        {
            typeof(DotsRuntimeBuildProfile),
            typeof(DotsRuntimeRootAssembly),
            typeof(OutputBuildDirectory)
        };

        public override BuildResult Run(BuildContext context)
        {
            var profile = context.GetComponentOrDefault<DotsRuntimeBuildProfile>();
            var rootAssembly = context.GetComponentOrDefault<DotsRuntimeRootAssembly>();
            var targetName = rootAssembly.MakeBeeTargetName(context.BuildConfigurationName);
            var workingDir = DotsRuntimeRootAssembly.BeeRootDirectory;
            var outputDir = new DirectoryInfo(BuildStepGenerateBeeFiles.GetFinalOutputDirectory(context, targetName));

            var result = BeeTools.Run(targetName, workingDir, context.BuildProgress);
            outputDir.Combine("Logs").GetFile("BuildLog.txt").WriteAllText(result.Output);
            workingDir.GetFile("runbuild" + ShellScriptExtension()).UpdateAllText(result.Command);

            if (result.Failed)
            {
                return context.Failure(result.Error);
            }

            if (!string.IsNullOrEmpty(rootAssembly.ProjectName))
            {
                var outputTargetFile = outputDir.GetFile(rootAssembly.ProjectName + profile.Target.ExecutableExtension);
                context.SetValue(new DotsRuntimeBuildArtifact { OutputTargetFile = outputTargetFile });
            }

            return context.Success();
        }

        string ShellScriptExtension()
        {
#if UNITY_EDITOR_WIN
            return ".bat";
#else
            return ".sh";
#endif
        }
    }
}
