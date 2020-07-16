using System;
using System.Linq;
using Unity.Build;
using Unity.Build.Common;
using Unity.Build.DotsRuntime;
using Unity.Serialization.Json;

namespace Unity.Entities.Runtime.Build
{
    [BuildStep(Description = "Generating Bee Files")]
    sealed class BuildStepGenerateBeeFiles : BuildStepBase
    {
        public static readonly int BuildSettingsFileVersion = 1;

        public override Type[] UsedComponents { get; } =
        {
            typeof(DotsRuntimeBuildProfile),
            typeof(DotsRuntimeRootAssembly),
            typeof(OutputBuildDirectory),
            typeof(IDotsRuntimeBuildModifier)
        };

        public override BuildResult Run(BuildContext context)
        {
            // BuildConfiguration names are used as part of a generated Bee target name. As such, error if the user's
            // asset contains a space as this will be seen by bee as multiple target names erroneously
            if (context.BuildConfigurationName.Contains(' '))
                throw new ArgumentException($"The DOTS Runtime Build Profile does not support BuildConfiguration assets with spaces in the name. Please rename asset '{context.BuildConfigurationName}'");

            var manifest = context.BuildManifest;
            var profile = context.GetComponentOrDefault<DotsRuntimeBuildProfile>();
            var rootAssembly = context.GetComponentOrDefault<DotsRuntimeRootAssembly>();
            var outputDir = DotsRuntimeRootAssembly.BeeRootDirectory;

            BuildProgramDataFileWriter.WriteAll(outputDir.FullName);

            var jsonObject = new JsonObject();
            var targetName = rootAssembly.MakeBeeTargetName(context.BuildConfigurationName);

            jsonObject["Version"] = BuildSettingsFileVersion;
            jsonObject["PlatformTargetIdentifier"] = profile.Target.BeeTargetName;
#if UNITY_2020_1_OR_NEWER
            jsonObject["RootAssembly"] = rootAssembly.RootAssembly.asset.name;
#else
            jsonObject["RootAssembly"] = rootAssembly.RootAssembly.name;
#endif

            // Managed debugging is disabled by default. It can be enabled
            // using the IL2CPPSettings object, which implements IDotsRuntimeBuildModifier.
            // See the code below which reads that object.
            jsonObject["EnableManagedDebugging"] = BuildSettingToggle.UseBuildConfiguration.ToString();

            // EnableBurst is defaulted to true but can be configured via the DotsRuntimeBurstSettings object
            jsonObject["EnableBurst"] = true;

            // Scripting Settings defaults but can be overriden via the DotsRuntimeScriptingSettings object
            jsonObject["EnableMultithreading"] = false;
            jsonObject["EnableSafetyChecks"] = BuildSettingToggle.UseBuildConfiguration.ToString();
            jsonObject["EnableProfiler"] = BuildSettingToggle.UseBuildConfiguration.ToString();

            jsonObject["FinalOutputDirectory"] = GetFinalOutputDirectory(context, targetName);
            jsonObject["DotsConfig"] = profile.Configuration.ToString();

            foreach (var component in context.GetComponents<IDotsRuntimeBuildModifier>())
            {
                component.Modify(jsonObject);
            }

            var settingsDir = new NPath(outputDir.FullName).Combine("settings");
            var json = JsonSerialization.ToJson(jsonObject, new JsonSerializationParameters
            {
                DisableRootAdapters = true,
                SerializedType = typeof(JsonObject)
            });
            settingsDir.Combine($"{targetName}.json").UpdateAllText(json);

            var file = rootAssembly.StagingDirectory.Combine(targetName).GetFile("export.manifest");
            file.UpdateAllLines(manifest.ExportedFiles.Select(x => x.FullName).ToArray());

            profile.Target.WriteBeeConfigFile(DotsRuntimeRootAssembly.BeeRootDirectory.ToString());

            return context.Success();
        }

        public static string GetFinalOutputDirectory(BuildContext context, string beeTargetName)
        {
            if (context.TryGetComponent<OutputBuildDirectory>(out var value))
            {
                return value.OutputDirectory;
            }
            return $"Builds/{beeTargetName}";
        }
    }
}
