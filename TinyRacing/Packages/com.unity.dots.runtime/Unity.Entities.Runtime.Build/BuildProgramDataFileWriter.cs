using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Serialization.Json;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Unity.Entities.Runtime.Build
{
    internal static class BuildProgramDataFileWriter
    {
        // Writing asmdefs takes an annoying long time. Let's rely on the fact that it is impossible to change your asmdef layout without
        // a scripting domain reload happening. A domain reload will cause this static field to be set back to false, so if we see it
        // is true, we know that things haven't changed, so we can stop spending 300ms figuring out where all asmdefs live.
        private static bool s_AlreadyWrittenDataFile = false;

        private class AsmDefJsonObject
        {
            [SerializeField] public string name = null;
        }

        private class AsmRefJsonObject
        {
            [SerializeField] public string reference = null;
        }

        private class ProjectManifestJsonObject
        {
            [SerializeField] public List<string> testables = null;
        }

        public static void WriteAll(string directory,
            string selectedConfig = null)
        {
            WriteAsmdefsJson(directory);
            WriteBeeConfigFile(directory);
            WriteBeeBatchFile(directory);
            if (selectedConfig != null)
            {
                WriteSelectedConfigFile(directory, selectedConfig);
            }
        }

        private static void WriteSelectedConfigFile(NPath directory, string selectedConfig)
        {
            var file = directory.Combine("selectedconfig.json").MakeAbsolute();
            file.UpdateAllText(JsonSerialization.ToJson(new SelectedConfigJson()
            {
                Config = selectedConfig
            }));
        }

        private static void WriteBeeBatchFile(NPath directory)
        {
            var file = directory.Combine("bee");

            // Then write out some helper bee/bee.cmd scripts
            using (StreamWriter sw = new StreamWriter(file.ToString()))
            {
                sw.NewLine = "\n";
                sw.WriteLine($@"#!/bin/sh");
                sw.WriteLine();
                sw.WriteLine("MONO=");
                sw.WriteLine($@"BEE=""$PWD/{BeePath.RelativeTo(directory).ToString(SlashMode.Forward)}""");
                sw.WriteLine("BEE=$(printf %q \"$BEE\")");
                sw.WriteLine($@"if [ ""$APPDATA"" == """" ] ; then");
                sw.WriteLine("    MONO=mono");
                sw.WriteLine("fi");
                sw.WriteLine("cmdToRun=\"${MONO} ${BEE} $*\"");
                sw.WriteLine("if [ $# -eq 0 ]; then");
                sw.WriteLine("    eval \"${cmdToRun} -t\"");
                sw.WriteLine("  else");
                sw.WriteLine("    eval \"${cmdToRun}\"");
                sw.WriteLine("fi");
            }

            var cmdFile = directory.Combine("bee.cmd");
            using (StreamWriter sw = new StreamWriter(cmdFile.ToString()))
            {
                sw.NewLine = "\n";
                sw.WriteLine("@ECHO OFF");
                sw.WriteLine($@"set bee=%~dp0{BeePath.RelativeTo(directory).ToString(SlashMode.Backward)}");
                sw.WriteLine($@"if [%1] == [] (%bee% -t) else (%bee% %*)");
            }
        }

        private static NPath BeePath { get; } = Path.GetFullPath($"{Constants.DotsRuntimePackagePath}/bee~/bee.exe");

        static void WriteAsmdefsJson(NPath directory)
        {
            var file = directory.Combine("asmdefs.json");
            if (file.FileExists() && s_AlreadyWrittenDataFile)
            {
            }

            var asmdefs = AllAssemblyDefinitions().ToList();

            var asmrefs = new List<AsmRefDescription>();
            foreach (var asmrefFile in AllAsmRefs())
            {
                var asmref = JsonUtility.FromJson<AsmRefJsonObject>(asmrefFile.MakeAbsolute().ReadAllText());
                var packageInfo = PackageInfo.FindForAssetPath(asmrefFile.ToString());
                var packageSource = packageInfo?.source.ToString() ?? "NoPackage";
                asmrefs.Add(new AsmRefDescription()
                {
                    AsmRefTarget = asmref.reference,
                    FullPath = Path.GetFullPath(asmrefFile.ToString()),
                    PackageSource = packageSource
                });
            }

            var projectPath = new NPath(UnityEngine.Application.dataPath).Parent;
            var projectManifestPath = projectPath.Combine("Packages/manifest.json");
            var projectManifest = JsonUtility.FromJson<ProjectManifestJsonObject>(projectManifestPath.MakeAbsolute().ReadAllText());
            List<string> testableAsmDefNames = new List<string>();
            foreach (var testablePackageName in projectManifest.testables)
            {
                testableAsmDefNames.AddRange(asmdefs.Where(a => a.AsmdefName.EndsWith(".Tests") && a.FullPath.Contains(testablePackageName)).Select(a => a.AsmdefName));
            }

            var compilationPipelinePath = new NPath(Path.Combine(EditorApplication.applicationContentsPath, "Managed", "Unity.CompilationPipeline.Common.dll"));
            file.UpdateAllText(JsonSerialization.ToJson(new BeeAsmdefConfiguration()
            {
                asmdefs = asmdefs,
                asmrefs = asmrefs,
                UnityProjectPath = projectPath.ToString(),
                ProjectName = projectPath.FileName,
                Testables = testableAsmDefNames,
                CompilationPipelineAssemblyPath = compilationPipelinePath.ToString(),
                BuildSettingsFileVersion = BuildStepGenerateBeeFiles.BuildSettingsFileVersion
            }));
            s_AlreadyWrittenDataFile = true;
        }

        internal delegate string ResolvePackagePathHandler(string absolutePath);

        /// <summary>
        /// Allows internal development tools to resolve known symlinks in package file paths before feeding them
        /// to the solution generation logic.
        /// </summary>
        internal static event ResolvePackagePathHandler resolvePackagePath;

        static string ResolvePackagePath(string absolutePath)
        {
            var resolver = resolvePackagePath;
            return resolver != null ? resolver(absolutePath) : absolutePath;
        }

        internal static IEnumerable<AsmDefDescription> AllAssemblyDefinitions()
        {
            var guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
            var ret = new List<AsmDefDescription>();
            foreach (var guid in guids)
            {
                var asmdefFile = AssetDatabase.GUIDToAssetPath(guid);

                var fullpath = new NPath(asmdefFile).MakeAbsolute();
                var asmdef = JsonUtility.FromJson<AsmDefJsonObject>(fullpath.ReadAllText());
                var packageInfo = PackageInfo.FindForAssetPath(asmdefFile.ToString());
                var packageSource = packageInfo?.source.ToString() ?? "NoPackage";
                // this creates a world of problems
                //if (AssemblyDefinitionUtility.IsRuntimeAssembly(path))
                ret.Add(new AsmDefDescription
                {
                    AsmdefName = asmdef.name,
                    FullPath = ResolvePackagePath(Path.GetFullPath(fullpath.ToString())),
                    Guid = guid,
                    PackageSource = packageSource
                });
            }

            return ret.OrderBy(asmdef => new NPath(asmdef.FullPath).RelativeTo(new NPath(Application.dataPath).Parent));
        }

        internal static IEnumerable<NPath> AllAsmRefs()
        {
            var paths = new HashSet<string>();
            var guids = AssetDatabase.FindAssets("t:AssemblyDefinitionReferenceAsset");
            foreach (var guid in guids)
            {
                var asmdefPath = AssetDatabase.GUIDToAssetPath(guid);

                paths.Add(asmdefPath);
            }

            foreach (var path in paths.OrderBy(p => p))
            {
                yield return new NPath(path);
            }
        }

        private struct BeeAsmdefConfiguration
        {
            public List<AsmDefDescription> asmdefs;
            public List<AsmRefDescription> asmrefs;
            public string UnityProjectPath;
            public string ProjectName;
            public List<string> Testables;
            public string CompilationPipelineAssemblyPath;
            public int BuildSettingsFileVersion;
        }

        internal struct AsmDefDescription
        {
            public string AsmdefName;
            public string FullPath;
            public string PackageSource;
            public string Guid;
        }

        private struct AsmRefDescription
        {
            public string AsmRefTarget;
            public string FullPath;
            public string PackageSource;
        }

        static void WriteBeeConfigFile(NPath directory)
        {
            var file = directory.Combine("bee.config");
            file.UpdateAllText(JsonSerialization.ToJson(new BeeConfig
            {
                BuildProgramBuildProgramFiles = new List<string>
                {
                    Path.GetFullPath($"{Constants.DotsRuntimePackagePath}/bee~/BuildProgramBuildProgramSources")
                },
                MultiDag = true
            }));
        }

        private struct BeeConfig
        {
            public List<string> BuildProgramBuildProgramFiles;
            public bool MultiDag;
        }
    }

    internal class SelectedConfigJson
    {
        public string Config;
    }
}
