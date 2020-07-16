using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Build;
using Unity.Build.Common;
using Unity.Build.Internals;
using Unity.Scenes;
using Unity.Scenes.Editor;
using UnityEngine.SceneManagement;

namespace Unity.Entities.Runtime.Build
{
    [BuildStep(Description = "Exporting Entities")]
    sealed class BuildStepExportEntities : BuildStepBase
    {
        public BlobAssetStore m_BlobAssetStore;

        public override Type[] UsedComponents { get; } =
        {
            typeof(DotsRuntimeBuildProfile),
            typeof(DotsRuntimeRootAssembly),
            typeof(SceneList)
        };

        public override BuildResult Run(BuildContext context)
        {
            var manifest = context.BuildManifest;
            var profile = context.GetComponentOrDefault<DotsRuntimeBuildProfile>();
            var rootAssembly = context.GetComponentOrDefault<DotsRuntimeRootAssembly>();
            var buildScenes = context.GetComponentOrDefault<SceneList>();

            var exportedSceneGuids = new HashSet<Guid>();

            var originalActiveScene = SceneManager.GetActiveScene();

#if USE_INCREMENTAL_CONVERSION
            m_BlobAssetStore = new BlobAssetStore();
#endif

            void ExportSceneToFile(Scene scene, Guid guid)
            {
                var targetName = rootAssembly.MakeBeeTargetName(context.BuildConfigurationName);
                var dataDirectory = rootAssembly.StagingDirectory.Combine(targetName).Combine("Data");
                var outputFile = dataDirectory.GetFile(guid.ToString("N"));
                using (var exportWorld = new World("Export World"))
                {
                    var config = BuildContextInternals.GetBuildConfiguration(context);
#if USE_INCREMENTAL_CONVERSION
                    var exportDriver = new TinyExportDriver(config, dataDirectory, exportWorld, m_BlobAssetStore);
#else
                    var exportDriver = new TinyExportDriver(config, dataDirectory);
#endif
                    exportDriver.DestinationWorld = exportWorld;
                    exportDriver.SceneGUID = new Hash128(guid.ToString("N"));

                    SceneManager.SetActiveScene(scene);

                    GameObjectConversionUtility.ConvertScene(scene, exportDriver);
                    context.GetOrCreateValue<WorldExportTypeTracker>()?.AddTypesFromWorld(exportWorld);

                    WorldExport.WriteWorldToFile(exportWorld, outputFile);
                    exportDriver.Write(manifest);
                }

                manifest.Add(guid, scene.path, outputFile.ToSingleEnumerable());
            }

            foreach (var rootScenePath in buildScenes.GetScenePathsForBuild())
            {
                using (var loadedSceneScope = new LoadedSceneScope(rootScenePath))
                {
                    var thisSceneSubScenes = loadedSceneScope.ProjectScene.GetRootGameObjects()
                        .Select(go => go.GetComponent<SubScene>())
                        .Where(g => g != null && g);

                    foreach (var subScene in thisSceneSubScenes)
                    {
                        var guid = new Guid(subScene.SceneGUID.ToString());
                        if (exportedSceneGuids.Contains(guid))
                            continue;

                        var isLoaded = subScene.IsLoaded;
                        if (!isLoaded)
                            SubSceneInspectorUtility.EditScene(subScene);

                        var scene = subScene.EditingScene;
                        var sceneGuid = subScene.SceneGUID;

                        ExportSceneToFile(scene, guid);

                        if (!isLoaded)
                            SubSceneInspectorUtility.CloseSceneWithoutSaving(subScene);
                    }
                }
            }

            SceneManager.SetActiveScene(originalActiveScene);

#if USE_INCREMENTAL_CONVERSION
            m_BlobAssetStore.Dispose();
#endif

            return context.Success();
        }
    }
}
