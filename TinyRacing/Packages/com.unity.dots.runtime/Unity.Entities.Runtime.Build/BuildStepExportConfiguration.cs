using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Build;
using Unity.Build.Common;
using Unity.Build.Internals;

namespace Unity.Entities.Runtime.Build
{
    [BuildStep(Description = "Exporting Configuration")]
    sealed class BuildStepExportConfiguration : BuildStepBase
    {
        public override Type[] UsedComponents { get; } =
        {
            typeof(DotsRuntimeBuildProfile),
            typeof(DotsRuntimeRootAssembly),
            typeof(SceneList),
            typeof(OutputBuildDirectory)
        };

        void WriteDebugFile(BuildContext context, BuildManifest manifest, DotsRuntimeBuildProfile profile)
        {
            var rootAssembly = context.GetComponentOrDefault<DotsRuntimeRootAssembly>();
            var targetName = rootAssembly.MakeBeeTargetName(context.BuildConfigurationName);
            var outputDir = BuildStepGenerateBeeFiles.GetFinalOutputDirectory(context, targetName);
            var debugFile = new NPath(outputDir).Combine("Logs/SceneExportLog.txt");
            var debugAssets = manifest.Assets.OrderBy(x => x.Value)
                .Select(x => $"{x.Key.ToString("N")} = {x.Value}").ToList();

            var debugLines = new List<string>();

            debugLines.Add("::Exported Assets::");
            debugLines.AddRange(debugAssets);
            debugLines.Add("\n");

            // Write out a separate list of types that we see in the dest world
            // as well as all types
            for (int group = 0; group < 2; ++group)
            {
                IEnumerable<TypeManager.TypeInfo> typesToWrite;
                if (group == 0)
                {
                    var typeTracker = context.GetOrCreateValue<WorldExportTypeTracker>();
                    if (typeTracker == null)
                        continue;
                    typesToWrite = typeTracker.TypesInUse.Select(t =>
                        TypeManager.GetTypeInfo(TypeManager.GetTypeIndex(t)));

                    //Verify if an exported type is included in the output, if not print error message
                    foreach (TypeManager.TypeInfo exportedType in typesToWrite)
                    {
                        if (!rootAssembly.TypeCache.HasType(exportedType.Type))
                            Debug.LogError($"The {exportedType.Type.Name} component is defined in the {exportedType.Type.Assembly.GetName().Name} assembly, but that assembly is not referenced by the current build configuration. Either add it as a reference, or ensure that the conversion process that is adding that component does not run.");
                    }
                    debugLines.Add($"::Exported Types (by stable hash)::");
                }
                else
                {
                    typesToWrite = TypeManager.AllTypes;
                    debugLines.Add($"::All Types in TypeManager (by stable hash)::");
                }

                var debugTypeHashes = typesToWrite.OrderBy(ti => ti.StableTypeHash)
                    .Where(ti => ti.Type != null).Select(ti =>
                        $"0x{ti.StableTypeHash:x16} - {ti.StableTypeHash,22} - {ti.Type.FullName}");

                debugLines.AddRange(debugTypeHashes);
                debugLines.Add("\n");
            }

            debugFile.MakeAbsolute().WriteAllLines(debugLines.ToArray());
        }

        public override BuildResult Run(BuildContext context)
        {
            var manifest = context.BuildManifest;
            var profile = context.GetComponentOrDefault<DotsRuntimeBuildProfile>();
            var rootAssembly = context.GetComponentOrDefault<DotsRuntimeRootAssembly>();
            var targetName = rootAssembly.MakeBeeTargetName(context.BuildConfigurationName);
            var scenes = context.GetComponentOrDefault<SceneList>();
            var firstScene = scenes.GetScenePathsForBuild().FirstOrDefault();

            using (var loadedSceneScope = new LoadedSceneScope(firstScene))
            {
                var projectScene = loadedSceneScope.ProjectScene;

                using (var tmpWorld = new World(ConfigurationScene.Guid.ToString("N")))
                {
                    var em = tmpWorld.EntityManager;

                    // Run configuration systems
                    ConfigurationSystemGroup configSystemGroup = tmpWorld.GetOrCreateSystem<ConfigurationSystemGroup>();
                    var systems = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(ConfigurationSystemBase));
                    foreach (var type in systems)
                    {
                        ConfigurationSystemBase baseSys = (ConfigurationSystemBase)tmpWorld.GetOrCreateSystem(type);
                        baseSys.projectScene = projectScene;
                        baseSys.buildConfiguration = BuildContextInternals.GetBuildConfiguration(context);
                        configSystemGroup.AddSystemToUpdateList(baseSys);
                    }
                    configSystemGroup.SortSystemUpdateList();
                    configSystemGroup.Update();

                    // Export configuration scene
                    var outputFile = rootAssembly.StagingDirectory.Combine(targetName)
                        .Combine("Data")
                        .GetFile(tmpWorld.Name);
                    context.GetOrCreateValue<WorldExportTypeTracker>()?.AddTypesFromWorld(tmpWorld);
                    WorldExport.WriteWorldToFile(tmpWorld, outputFile);

                    // Update manifest
                    manifest.Add(ConfigurationScene.Guid, ConfigurationScene.Path, outputFile.ToSingleEnumerable());

                    // Dump debug file
                    WriteDebugFile(context, manifest, profile);
                }
            }
            return context.Success();
        }
    }
}
