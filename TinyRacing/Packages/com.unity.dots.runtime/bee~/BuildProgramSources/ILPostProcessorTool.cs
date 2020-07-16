using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Bee.Core;
using Bee.CSharpSupport;
using Bee.DotNet;
using Bee.Tools;
using NiceIO;
using Unity.BuildSystem.CSharpSupport;
using Unity.BuildTools;

static class ILPostProcessorTool
{
    private static readonly Lazy<DotNetRunnableProgram> _ILPostProcessorRunnableProgram = new Lazy<DotNetRunnableProgram>(() =>
    {
        return new DotNetRunnableProgram(ILPostProcessorRunnerProgram.SetupSpecificConfiguration(DotsConfigs.HostDotnet));
    });

    public static CSharpProgram ILPostProcessorRunnerProgram => _ilPostProcessorRunnerProgram.Value;
    private static readonly Lazy<CSharpProgram> _ilPostProcessorRunnerProgram = new Lazy<CSharpProgram>(() =>
    {
        var ilppRunnerDir = BuildProgram.BeeRoot.Parent.Combine("ILPostProcessing~/ILPostProcessorRunner");

        // Since we will be running ILPostProcessors in a separate executable, we need to pull in the assembly references from the execution
        // environment the ILPostProcessors would have run in normally (i.e. the editor)
        var assemblyReferences = new List<NPath>();
        foreach (var ilppAsm in BuildProgram.ILPostProcessorAssemblies)
        {
            foreach (var asm in ilppAsm.RecursiveRuntimeDependenciesIncludingSelf)
            {
                if ((asm.Path.FileNameWithoutExtension != "mscorlib" &&
                     asm.Path.FileNameWithoutExtension != "netstandard") &&
                    asm.Path.FileNameWithoutExtension != BuildProgram.UnityCompilationPipeline.Path.FileNameWithoutExtension)
                    assemblyReferences.Add(asm.Path);
            }
        }

        var commandlineAsm = new DotNetAssembly(ilppRunnerDir.Combine("thirdparty/CommandLine.dll"), Framework.Framework461);

        var ilPostProcessorRunner = new CSharpProgram()
        {
            FileName = "ILPostProcessorRunner.exe",
            Sources = { ilppRunnerDir },
            Unsafe = true,
            LanguageVersion = "7.3",
            References =
            {
                commandlineAsm, BuildProgram.UnityCompilationPipeline
            },
            ProjectFilePath = "ILPostProcessorRunner.csproj",
            Framework = { Framework.Framework471 },
            IgnoredWarnings = { 3270 },
            SupportFiles = { assemblyReferences },
        };

        return ilPostProcessorRunner;
    });

    public static DotNetAssembly SetupInvocation(
        DotNetAssembly inputAssembly,
        CSharpProgramConfiguration config,
        string[] defines)
    {
        return inputAssembly.ApplyDotNetAssembliesPostProcessor($"artifacts/{inputAssembly.Path.FileNameWithoutExtension}/{config.Identifier}/post_ilprocessing/",
            (inputAssemblies, targetDirectory) => AddActions(config, inputAssemblies, targetDirectory, defines)
        );
    }

    private static void AddActions(CSharpProgramConfiguration config, DotNetAssembly[] inputAssemblies, NPath targetDirectory, string[] defines)
    {
        var processors = BuildProgram.ILPostProcessorAssemblies.Select(asm => asm.Path.MakeAbsolute());
        var outputDirArg = "--outputDir " + targetDirectory.MakeAbsolute().QuoteForProcessStart();
        var processorPathsArg = processors.Count() > 0 ? "--processors " + processors.Select(p => p.QuoteForProcessStart()).Aggregate((s1, s2) => s1 + "," + s2) : "";

        var referenceAssemblyProvider = ReferenceAssemblyProvider.Default;

        foreach (var inputAsm in inputAssemblies.OrderByDependencies())
        {
            var assemblyArg = inputAsm.Path.MakeAbsolute().QuoteForProcessStart();
            var referenceAsmPaths = inputAsm.RuntimeDependencies.Where(a => !a.Path.IsChildOf("post_ilprocessing"))
                .Select(a => a.Path.MakeAbsolute());

            var dotsConfig = (DotsRuntimeCSharpProgramConfiguration)config;

            switch (dotsConfig.TargetFramework)
            {
                case TargetFramework.Tiny:
                    {
                        referenceAsmPaths = referenceAsmPaths.Concat(
                            new[]
                            {
                                Il2Cpp.Distribution.Path.Combine("build/profiles/Tiny/Facades/netstandard.dll"),
                                Il2Cpp.TinyCorlib.Path
                            });
                        break;
                    }

                case TargetFramework.NetStandard20:
                    {
                        NPath bclDir = Il2Cpp.Il2CppDependencies.Path.Combine("MonoBleedingEdge/builds/monodistribution/lib/mono/unityaot");
                        referenceAsmPaths = referenceAsmPaths.Concat(new[] { bclDir.Combine("mscorlib.dll"), bclDir.Combine("Facades/netstandard.dll") });
                        break;
                    }

                default:
                    throw new NotImplementedException($"Unknown target framework: {dotsConfig.TargetFramework}");
            }

            var referencesArg = referenceAsmPaths.Any() ? "--assemblyReferences " + referenceAsmPaths.Select(r => r.MakeAbsolute().QuoteForProcessStart()).Distinct().Aggregate((s1, s2) => s1 + "," + s2) : string.Empty;
            var allscriptDefines = dotsConfig.Defines.Concat(defines);
            var definesArg = !allscriptDefines.Empty() ? "--scriptingDefines " + allscriptDefines.Distinct().Aggregate((d1, d2) => d1 + "," + d2) : "";
            var targetFiles = TargetPathsFor(targetDirectory, inputAsm).ToArray();
            var inputFiles = InputPathsFor(inputAsm).Concat(processors).Concat(new[] { _ILPostProcessorRunnableProgram.Value.Path }).Concat(referenceAsmPaths).ToArray();

            var args = new List<string>
            {
                assemblyArg,
                outputDirArg,
                processorPathsArg,
                referencesArg,
                definesArg
            }.ToArray();

            Backend.Current.AddAction($"ILPostProcessorRunner",
                targetFiles,
                inputFiles,
                _ILPostProcessorRunnableProgram.Value.InvocationString,
                args,
                allowedOutputSubstrings: new[] { "ILPostProcessorRunner", "[WARN]", "[ERROR]" }
            );
        }
    }

    private static IEnumerable<NPath> TargetPathsFor(NPath targetDirectory, DotNetAssembly inputAssembly)
    {
        yield return targetDirectory.Combine(inputAssembly.Path.FileName);
        if (inputAssembly.DebugSymbolPath != null)
            yield return targetDirectory.Combine(inputAssembly.DebugSymbolPath.FileName);
    }

    private static IEnumerable<NPath> InputPathsFor(DotNetAssembly inputAssembly)
    {
        yield return inputAssembly.Path;
        if (inputAssembly.DebugSymbolPath != null)
            yield return inputAssembly.DebugSymbolPath;
    }
}
