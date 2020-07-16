using System;
using System.Linq;
using Bee.Core;
using Bee.DotNet;
using NiceIO;
using Unity.BuildSystem.CSharpSupport;

class UnsafeUtility
{
    private static readonly Lazy<CSharpProgram> _unsafeUtility = new Lazy<CSharpProgram>(() =>
    {
        var program = new UnsafeUtilityCSharpProgram() {
            FileName = "UnsafeUtility.dll",
            Sources = { $"{BuildProgram.LowLevelRoot}/UnsafeUtility" },
            LanguageVersion = "7.3",
            Unsafe = true,
            ProjectFilePath = $"UnsafeUtility.csproj",
            CopyReferencesNextToTarget = false
        };

        program.References.Add((config) => GetTargetFramework(config) == TargetFramework.Tiny, Il2Cpp.TinyCorlib);
        program.Framework.Add((config) => GetTargetFramework(config) == TargetFramework.Tiny, Framework.FrameworkNone);
        

        program.Framework.Add(
            (config) => GetTargetFramework(config) == TargetFramework.NetStandard20,
            BuildProgram.HackedFrameworkToUseForProjectFilesIfNecessary);//NetStandard20);

        return program;
    });

    private static TargetFramework GetTargetFramework(CSharpProgramConfiguration config)
    {
        if (config is DotsRuntimeCSharpProgramConfiguration dotsConfig)
            return dotsConfig.TargetFramework;

        return TargetFramework.Tiny;
    }

    class UnsafeUtilityCSharpProgram : CSharpProgram
    {
        public override DotNetAssembly SetupSpecificConfiguration(CSharpProgramConfiguration config)
        {
            var nonPatchedUnsafeUtility = base.SetupSpecificConfiguration(config);
            
            var builtPatcher = new CSharpProgram() {
                Path = "artifacts/UnsafeUtilityPatcher/UnsafeUtilityPatcher.exe",
                Sources = { $"{BuildProgram.LowLevelRoot}/UnsafeUtilityPatcher" },
                Defines = { "NDESK_OPTIONS" },
                References =
                {
                    ReferenceAssemblies471.Paths,
                    MonoCecil.Paths,
                },
                LanguageVersion = "7.3"
            }.SetupDefault();

            var outDir = nonPatchedUnsafeUtility.Path.Parent.Combine("patched");
            NPath nPath = outDir.Combine(nonPatchedUnsafeUtility.Path.FileName);

            var builtPatcherProgram = new DotNetRunnableProgram(builtPatcher);
            var args = new[] 
            {
                $"--output={nPath}",
                $"--assembly={nonPatchedUnsafeUtility.Path}",
            };

            var result = new DotNetAssembly(nPath, nonPatchedUnsafeUtility.Framework,
                nonPatchedUnsafeUtility.DebugFormat,
                nPath.ChangeExtension("pdb"), nonPatchedUnsafeUtility.RuntimeDependencies,
                nonPatchedUnsafeUtility.ReferenceAssemblyPath);

            Backend.Current.AddAction("Patch", result.Paths,
                nonPatchedUnsafeUtility.Paths.Concat(builtPatcher.Paths).ToArray(), builtPatcherProgram.InvocationString,
                args);

            return result;
        }
    }

    public static CSharpProgram Program => _unsafeUtility.Value;
}