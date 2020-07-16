using System.IO;
using Unity.Build.Common;
using Unity.Build.Editor;
using Unity.Entities.Conversion;
using UnityEditor;

namespace Unity.Entities.Runtime.Build
{
    static class MenuItemDotsRuntime
    {
        const string k_CreateBuildConfigurationAssetDotsRuntime = BuildConfigurationMenuItem.k_BuildConfigurationMenu + "DOTS Runtime Build Configuration";

        [MenuItem(k_CreateBuildConfigurationAssetDotsRuntime, true)]
        static bool CreateBuildConfigurationAssetDotsRuntimeValidation()
        {
            return Directory.Exists(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        [MenuItem(k_CreateBuildConfigurationAssetDotsRuntime)]
        static void CreateBuildConfigurationAssetDotsRuntime()
        {
            Selection.activeObject = BuildConfigurationMenuItem.CreateAssetInActiveDirectory("DotsRuntime",
                new GeneralSettings(),
                new SceneList(),
                new ConversionSystemFilterSettings(),
                new DotsRuntimeBuildProfile
                {
                    Pipeline = new DotsRuntimeBuildPipeline()
                });
        }
    }
}
