using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using Bee;
using Bee.Core;
using Bee.CSharpSupport;
using Bee.DotNet;
using Bee.Stevedore;
using Bee.Toolchain.Emscripten;
using Bee.Tools;
using Bee.TundraBackend;
using Bee.VisualStudioSolution;
using NiceIO;
using Unity.BuildSystem.CSharpSupport;
using Unity.BuildSystem.NativeProgramSupport;
using static Unity.BuildSystem.NativeProgramSupport.NativeProgramConfiguration;
using Bee.Toolchain.Extension;
using Bee.Toolchain.VisualStudio;
using Newtonsoft.Json.Linq;
using Unity.BuildTools;

public class BuildProgram
{
    public static NPath BeeRootValue;
    public static NPath LowLevelRoot => BeeRoot.Parent.Combine("LowLevelSupport~");
    public static DotsRuntimeCSharpProgram UnityTinyBurst { get; set; }
    public static DotsRuntimeCSharpProgram UnityLowLevel { get; set; }
    public static DotsRuntimeCSharpProgram ZeroJobs { get; set; }
    public static DotNetAssembly UnityCompilationPipeline { get; set; }
    public static DotNetAssembly NUnitFramework { get; set; }
    public static DotNetAssembly NUnitLite { get; set; }
    public static DotNetAssembly[] ILPostProcessorAssemblies { get; private set; }
    
    /*
     * HACK for right (03/25/2020) now until fixed sdk project files arrive from @andrews:
     * pretend to compile against netfw instead of netstandard if projectfiles are requested, because
     * otherwise bee will generate sdk style project files, which are broken with tiny. 
     */
    public static Framework HackedFrameworkToUseForProjectFilesIfNecessary => IsRequestedTargetExactlyProjectFiles()
        ? (Framework) Framework.Framework461
        : Framework.NetStandard20;

    public static Dictionary<string, List<DotsRuntimeCSharpProgramConfiguration>> PerConfigBuildSettings { get; set; } =
        new Dictionary<string, List<DotsRuntimeCSharpProgramConfiguration>>();
    public static VisualStudioSolution VisualStudioSolution { get; private set; }
    
    public static NPath BeeRoot
    {
        get {
            if (BeeRootValue == null)
                throw new InvalidOperationException("BeeRoot accessed before it has been initialized");
            return BeeRootValue;
        }
    }

    static bool CanSkipSetupOf(string programName, DotsRuntimeCSharpProgramConfiguration config)
    {
        /* This is disabled for now (11/12/2019) because we have a theory that because there will be very few
         * bee targets overall, we don't need to optimize project files as much as we used to.
         * But we should re-enable this check if we see project files taking too long to generate. 
         */
        /*if (IsRequestedTargetExactlyProjectFiles())
            return true;*/
        
        if (!IsRequestedTargetExactlySingleAppSingleConfig()) 
            return false;

        return config.Identifier != StandaloneBeeDriver.GetCommandLineTargets().Single();
    }

    public static bool IsRequestedTargetExactlyProjectFiles()
    {
        var commandLineTargets = StandaloneBeeDriver.GetCommandLineTargets();
        if (commandLineTargets.Count() != 1)
            return false;

        return commandLineTargets.Single() == "ProjectFiles";
    }
    
    private static bool IsRequestedTargetExactlySingleAppSingleConfig()
    {
        var commandLineTargets = StandaloneBeeDriver.GetCommandLineTargets();
        if (commandLineTargets.Count() != 1)
            return false;

        var commandLineTarget = commandLineTargets.Single();
        var ret = PerConfigBuildSettings.Any(entry => entry.Value.Any(v => v.Identifier == commandLineTarget));
        return ret;
    }

    static void Main()
    {
        if (!(Backend.Current is TundraBackend))
        {
            StandaloneBeeDriver.RunBuildProgramInBeeEnvironment("dummy.json", Main);
            return;
        }
        
        BeeRootValue = AsmDefConfigFile.AsmDefDescriptionFor("Unity.ZeroPlayer.TypeRegGen").Path.Parent.Parent.Combine("bee~");
        
        StevedoreGlobalSettings.Instance = new StevedoreGlobalSettings
        {
            // Manifest entries always override artifact IDs hard-coded in Bee
            // Setting EnforceManifest to true will also ensure no artifacts
            // are used without being listed in a manifest.
            EnforceManifest = true,
            Manifest =
            {
                BeeRootValue.Combine("manifest.stevedore"),
            },
        };

        //The stevedore global manifest will override DownloadableCsc.Csc72 artifacts and use Csc73
        CSharpProgram.DefaultConfig = new CSharpProgramConfiguration(CSharpCodeGen.Release, DownloadableCsc.Csc72);
        
        PerConfigBuildSettings = DotsConfigs.MakeConfigs();
        foreach (var rootAssemblyName in PerConfigBuildSettings.Keys)
        {
            AsmDefConfigFile.AsmDefDescriptionFor(rootAssemblyName).IsTinyRoot = true;
        }

        //any asmdef that sits next to a .project file we will consider a tiny game.
        var asmDefDescriptions = AsmDefConfigFile.AssemblyDefinitions.ToArray();
        var burstAsmDef = asmDefDescriptions.First(d => d.Name == "Unity.Burst");

        UnityLowLevel = new DotsRuntimeCSharpProgram($"{LowLevelRoot}/Unity.LowLevel")
        {
            References = { UnsafeUtility.Program },
            Unsafe = true
        };
        UnityLowLevel.NativeProgram.Libraries.Add(IsLinux, new SystemLibrary("dl"));
        UnityLowLevel.NativeProgram.Libraries.Add(c => c.Platform is AndroidPlatform, new SystemLibrary("log"));

        UnityTinyBurst = new DotsRuntimeCSharpProgram($"{LowLevelRoot}/Unity.Tiny.Burst")
        {
            References = { UnityLowLevel },
            Unsafe = true
        };

        ZeroJobs = new DotsRuntimeCSharpProgram($"{LowLevelRoot}/Unity.ZeroJobs")
        {
            References = { UnityLowLevel, UnityTinyBurst, GetOrMakeDotsRuntimeCSharpProgramFor(burstAsmDef) },
            Unsafe = true
        };
        
        UnityCompilationPipeline = new DotNetAssembly(
            AsmDefConfigFile.UnityCompilationPipelineAssemblyPath,
            HackedFrameworkToUseForProjectFilesIfNecessary);


        var nunit = new StevedoreArtifact("nunit-framework");
        Backend.Current.Register(nunit);
        NUnitLite = new DotNetAssembly(nunit.Path.Combine("bin", "net40", "nunitlite.dll"), Framework.Framework40);
        NUnitFramework = new DotNetAssembly(nunit.Path.Combine("bin", "net40", "nunit.framework.dll"), Framework.Framework40);

        BurstCompiler.BurstExecutable = GetBurstExecutablePath(burstAsmDef).QuoteForProcessStart();

        var ilPostProcessorPrograms = asmDefDescriptions
            .Where(d => d.Name.EndsWith(".CodeGen") && !d.DefineConstraints.Contains("!NET_DOTS"))
            .Select(GetOrMakeDotsRuntimeCSharpProgramFor);
        ILPostProcessorAssemblies = ilPostProcessorPrograms.Select(p =>
            {
                /*
                 * We want to compile the ilpp's for hostdotnet, even though we might be compiling the actual game
                 * for something else (e.g. wasm). The ilpp's may reference actual game assemblies, which may have
                 * native code. We do not want to set up the native code for those game assemblies for hostdotnet,
                 * because a) it makes no sense and b) the native toolchains might not be installed, and it would be
                 * dumb to require that to build for an unrelated platform.
                 *
                 * So, set the NativeProgramConfiguration to null, and set up with that. But first, set the platform,
                 * because normally the platform comes from the npc.
                 */
                var tmp = DotsConfigs.HostDotnet;
                tmp.Platform = DotsConfigs.HostDotnet.Platform;
                tmp.NativeProgramConfiguration = null;
                var ret = p.SetupSpecificConfiguration(tmp);
                return ret;
            })
            .ToArray();

        var tinyMainAsmDefs = asmDefDescriptions.Where(a=>a.IsTinyRoot);
        var gameAsmDefs = tinyMainAsmDefs.Union(AsmDefConfigFile.TestableAssemblyDefinitions);
        foreach (var gameAsmdef in gameAsmDefs)
        {
            var gameProgram = GetOrMakeDotsRuntimeCSharpProgramFor(gameAsmdef);
            if (gameProgram.AsmDefDescription.NeedsEntryPointAdded())
                gameProgram.References.Add(
                    GetOrMakeDotsRuntimeCSharpProgramFor(
                        AsmDefConfigFile.AsmDefDescriptionFor("Unity.Runtime.EntryPoint")));
        }

        var gamePrograms = gameAsmDefs.Select(SetupGame).ExcludeNulls().ToArray();

        var vs = new VisualStudioSolution
        {
            Path = AsmDefConfigFile.UnityProjectPath.Combine($"{AsmDefConfigFile.ProjectName}-Dots.sln").RelativeTo(NPath.CurrentDirectory),
            DefaultSolutionFolderFor = file => (file.Name.Contains("Unity.") || file.Name == "mscorlib") ? "Unity" : ""
        };

        var unityToolsFolder = "Unity/tools";
        var unityILPostProcessorsFolder = "Unity/ILPostProcessing";
        if (BeeRoot.IsChildOf(AsmDefConfigFile.UnityProjectPath))
        {
            var buildProjRef = new CSharpProjectFileReference("buildprogram.gen.csproj");
            vs.Projects.Add(buildProjRef, unityToolsFolder);
        }

        foreach (var gameProgram in gamePrograms)
            vs.Projects.Add(gameProgram);

        var toolPrograms = new[]
            { TypeRegistrationTool.EntityBuildUtils, TypeRegistrationTool.TypeRegProgram };
        foreach (var p in toolPrograms)
            vs.Projects.Add(p, unityToolsFolder);

        vs.Projects.Add(ILPostProcessorTool.ILPostProcessorRunnerProgram, unityILPostProcessorsFolder);
        foreach (var p in ilPostProcessorPrograms)
            vs.Projects.Add(p, unityILPostProcessorsFolder);

        foreach (var config in PerConfigBuildSettings.SelectMany(entry=>entry.Value))
        {
            //we want dotnet to be the default, and we cannot have nice things: https://aras-p.info/blog/2017/03/23/How-does-Visual-Studio-pick-default-config/platform/
            var solutionConfigName = config.Identifier == "dotnet" ? "Debug (dotnet)": config.Identifier;
            
            vs.Configurations.Add(new SolutionConfiguration(solutionConfigName, (configurations, file) =>
            {
                var firstOrDefault = configurations.FirstOrDefault(c => c == config);
                return new Tuple<IProjectConfiguration, bool>(
                    firstOrDefault ?? configurations.First(),
                    firstOrDefault != null || toolPrograms.Any(t=>t.ProjectFile == file));
            }));
        }

        VisualStudioSolution = vs;

        EditorToolsBuildProgram.Setup(BeeRoot);

        // Run this before solution setup, to potentially give this a chance to muck with the VisualStudioSolution
        DotsBuildCustomizer.RunAllCustomizers();

        if (!IsRequestedTargetExactlySingleAppSingleConfig())
            Backend.Current.AddAliasDependency("ProjectFiles", vs.Setup());
    }

    private static NPath GetBurstExecutablePath(AsmDefDescription burstAsmDef)
    {
        var burstDebugVariable = Environment.GetEnvironmentVariable("UNITY_BURST_RUNTIME_PATH");
        if (!string.IsNullOrEmpty(burstDebugVariable))
        {
            var bclPath = burstDebugVariable.ToNPath().Combine("bcl.exe");
            if (bclPath.FileExists())
                return bclPath;
        }

        return burstAsmDef.Path.Parent.Parent.Combine(".Runtime/bcl.exe");
    }

    private static bool IsTestProgramDotsRuntimeCompatible(DotsRuntimeCSharpProgram arg)
    {
        //We need a better way of knowing which asmdefs are supposed to work on dots-runtime, and which do not.  for now use a simple heuristic of "is it called Editor or is it called Hybrid"
        var allFileNames = Enumerable.Append(arg.References.ForAny().OfType<CSharpProgram>().Select(r => r.FileName), arg.FileName).ToArray();
        if (allFileNames.Any(f=>f.Contains("Editor")))
            return false;
        if (allFileNames.Any(f=>f.Contains("Hybrid")))
            return false;
        if (allFileNames.Any(f=>f.Contains("Unity.TextMeshPro")))
            return false;
        if (allFileNames.Any(f => f.Contains("Unity.ugui")))
            return false;
        //in theory, all tests for assemblies that are used by dotsruntime targetting programs should be dots runtime compatible
        //unfortunately we have some tests today that test dotsruntime compatible code,  but the testcode itself is not dotsruntime compatible.
        //blacklist these for now
        if (arg.FileName.Contains("Unity.Scenes.Tests"))
            return false; 
        if (arg.FileName.Contains("Unity.Build.Tests"))
            return false;
        if (arg.FileName.Contains("Unity.Build.Tests"))
            return false;
        if (arg.FileName.Contains("Unity.Authoring"))
            return false;
        if (arg.FileName.Contains("Unity.Serialization"))
            return false;
        if (arg.FileName.Contains("Unity.Entities.Reflection.Tests"))
            return false;
        if (arg.FileName.Contains("Unity.Properties"))
            return false;
        if (arg.FileName.Contains("Unity.Entities.Properties"))
            return false;
        if (arg.FileName.Contains("Unity.Burst.Tests"))
            return false;
        if (arg.FileName.Contains("Unity.jobs.Tests"))
            return false;
        if (arg.FileName.Contains("Unity.Collections.Tests"))
            return false;
        if (arg.FileName.Contains("Automation.Tests"))
            return false;
        if (arg.FileName.Contains("Unity.PerformanceTesting"))
            return false;
        if (arg.FileName.Contains(".CodeGen"))
            return false;
        if (arg.FileName.Contains("Unity.Entities.Determinism.Tests"))
            return false;
                
        return true;
    }

    static void SetupTestForConfig(string name, AsmDefCSharpProgram testProgram, DotsRuntimeCSharpProgramConfiguration config)
    {
        var builtTest = testProgram.SetupSpecificConfiguration(config);
        var postILProcessedTest = ILPostProcessorTool.SetupInvocation(builtTest, config, testProgram.Defines.For(config).ToArray());
        var postTypeRegGenTest = TypeRegistrationTool.SetupInvocation(postILProcessedTest, config);

        NPath deployDirectory = GameDeployDirectoryFor(testProgram, config);
        var deployed = postTypeRegGenTest.DeployTo(deployDirectory);

        testProgram.ProjectFile.OutputPath.Add(c => c == config, deployDirectory);
        testProgram.ProjectFile.BuildCommand.Add(c => c == config, new BeeBuildCommand(deployed.Path.ToString(), false, false).ToExecuteArgs());

        Backend.Current.AddAliasDependency($"{name.ToLower()}-{ config.Identifier}", deployed.Path);
        Backend.Current.AddAliasDependency("tests", deployed.Path);
    }
    
    //waiting for the burst release with BurstCompilerForLinux in Burst.bee.cs
    public class BurstCompilerForLinuxWaitingForBurstRelease : BurstCompiler
    {
        public override string TargetPlatform { get; set; } = "Linux";
    
        //--target=VALUE         Target CPU <Auto|X86_SSE2|X86_SSE4|X64_SSE2|X64_
        //    SSE4|AVX|AVX2|AVX512|WASM32|ARMV7A_NEON32|ARMV8A_
        //    AARCH64|THUMB2_NEON32> Default: Auto
        public override string TargetArchitecture { get; set; } = "X64_SSE2";
        public override string ObjectFormat { get; set; } = "Elf";
        public override string FloatPrecision { get; set; } = "High";
        public override bool SafetyChecks { get; set; } = true;
        public override bool DisableVectors { get; set; } = false;
        public override bool Link { get; set; } = false;
        public override string ObjectFileExtension { get; set; } = ".o";
        public override bool UseOwnToolchain { get; set; } = true;
    }
    private static DotsRuntimeCSharpProgram SetupGame(AsmDefDescription game)
    {
        var gameProgram = GetOrMakeDotsRuntimeCSharpProgramFor(game);
        var configToSetupGame = new Dictionary<DotsRuntimeCSharpProgramConfiguration, DotNetAssembly>();

        if (!PerConfigBuildSettings.ContainsKey(game.Name)) return null;

        var configsToUse = PerConfigBuildSettings[game.Name].Where(config => !CanSkipSetupOf(game.Name, config));
        foreach (var config in configsToUse)
        {
            var withoutExt =
                new NPath(new NPath(gameProgram.FileName).FileNameWithoutExtension).Combine(config.Identifier);
            NPath exportManifest = withoutExt.Combine("export.manifest");
            Backend.Current.RegisterFileInfluencingGraph(exportManifest);
            if (exportManifest.FileExists())
            {
                var dataFiles = exportManifest.MakeAbsolute().ReadAllLines();
                foreach (var dataFile in dataFiles.Select(d => new NPath(d)))
                    gameProgram.SupportFiles.Add(
                        c => c.Equals(config),
                        new DeployableFile(dataFile, "Data/" + dataFile.FileName));
            }

            gameProgram.ProjectFile.StartInfo.Add(
                c => c == config,
                StartInfoFor(config, EntryPointExecutableFor(gameProgram, config)));
            gameProgram.ProjectFile.BuildCommand.Add(
                c => c == config,
                new BeeBuildCommand(GameDeployBinaryFor(gameProgram, config).ToString(), false, false).ToExecuteArgs());
        }

        foreach (var config in configsToUse)
        {
            DotNetAssembly setupGame = gameProgram.SetupSpecificConfiguration(config);

            if (config.TargetFramework == TargetFramework.Tiny)
            {
                var tinyStandard = new DotNetAssembly(Il2Cpp.Distribution.Path.Combine("build/profiles/Tiny/Facades/netstandard.dll"), Framework.FrameworkNone);
                setupGame = setupGame.WithDeployables(tinyStandard);
            } 

            var postILProcessedGame = ILPostProcessorTool.SetupInvocation(
                setupGame,
                config,
                gameProgram.Defines.For(config).ToArray());
            var postTypeRegGenGame = TypeRegistrationTool.SetupInvocation(postILProcessedGame, config);
            configToSetupGame[config] = postTypeRegGenGame;
        }

        var il2CppOutputProgram = new Il2Cpp.Il2CppOutputProgram(gameProgram.AsmDefDescription.Name);
        
        var configToSetupGameBursted = new Dictionary<DotsRuntimeCSharpProgramConfiguration, DotNetAssembly>();

        foreach (var kvp in configToSetupGame)
        {
            var config = kvp.Key;
            var setupGame = kvp.Value;
            
            if (config.UseBurst)
            {
                
                BurstCompiler burstCompiler = null;
                if (config.Platform is WindowsPlatform)
                {
                    burstCompiler = new BurstCompilerForWindows64();
                    burstCompiler.Link = false;
                }
                else if (config.Platform is MacOSXPlatform)
                {
                    burstCompiler = new BurstCompilerForMac();
                    burstCompiler.Link = false;
                }
                else if (config.Platform is IosPlatform)
                {
                    burstCompiler = new BurstCompilerForiOS();
                    burstCompiler.EnableStaticLinkage = true;
                    burstCompiler.ObjectFileExtension = "a";
                }
                else if (config.Platform is LinuxPlatform)
                {
                    burstCompiler = new BurstCompilerForLinuxWaitingForBurstRelease();
                }
                else if (config.Platform is AndroidPlatform)
                {
                    burstCompiler = new BurstCompilerForAndroid();
                    burstCompiler.EnableStaticLinkage = false;
                    burstCompiler.Link = false;
                    burstCompiler.EnableDirectExternalLinking = true;
                }


                // Only generate marshaling info for platforms that require marshalling (e.g. Windows DotNet) 
                // but also if collection checks are enabled (as that is why we need marshalling)
                burstCompiler.EnableJobMarshalling &= config.EnableUnityCollectionsChecks;
                burstCompiler.SafetyChecks = config.EnableUnityCollectionsChecks;

                var outputDir = $"artifacts/{game.Name}/{config.Identifier}_bursted";
                var isWebGL = config.Platform is WebGLPlatform;
                var extension = config.NativeProgramConfiguration.ToolChain.DynamicLibraryFormat.Extension;

                //burst generates a .bundle on os x.
                if (config.Platform is MacOSXPlatform)
                    extension = "bundle";

                var burstLibName = "lib_burst_generated";
                var burstDynamicLib = new NativeProgram(burstLibName);
                DotNetAssembly burstedGame = setupGame;

                var burstlib = BurstCompiler.SetupBurstCompilationForAssemblies(
                    burstCompiler,
                    setupGame,
                    new NPath(outputDir).Combine("bclobj"),
                    outputDir,
                    burstLibName,
                    out burstedGame);
                if (config.Platform is IosPlatform || config.Platform is AndroidPlatform)
                {
                    il2CppOutputProgram.Libraries.Add(c => c.Equals(config.NativeProgramConfiguration), burstlib);
                    il2CppOutputProgram.Defines.Add(
                        c => c.Equals(config.NativeProgramConfiguration),
                        $"FORCE_PINVOKE_{burstLibName}_INTERNAL");
                }
                else
                {
                    burstDynamicLib.Libraries.Add(c => c.Equals(config.NativeProgramConfiguration), burstlib);
                    burstDynamicLib.Libraries.Add(
                        c => c.Equals(config.NativeProgramConfiguration),
                        gameProgram.TransitiveReferencesFor(config)
                            .Where(
                                p => p is DotsRuntimeCSharpProgram &&
                                     ((DotsRuntimeCSharpProgram) p).NativeProgram != null)
                            .Select(
                                p => new NativeProgramAsLibrary(((DotsRuntimeCSharpProgram) p).NativeProgram)
                                    {BuildMode = NativeProgramLibraryBuildMode.Dynamic}));
                    DotsRuntimeCSharpProgram.SetupDotsRuntimeNativeProgram(burstLibName, burstDynamicLib);

                    var builtBurstLib = burstDynamicLib.SetupSpecificConfiguration(
                        config.NativeProgramConfiguration,
                        config.NativeProgramConfiguration.ToolChain.DynamicLibraryFormat);
                    burstedGame = burstedGame.WithDeployables(builtBurstLib);
                }

                configToSetupGameBursted[config] = burstedGame;
            }
            else
            {
                configToSetupGameBursted[config] = setupGame;
            }
        }

        var configToSetupGameStripped = new Dictionary<DotsRuntimeCSharpProgramConfiguration, DotNetAssembly>();
        foreach (var kvp in configToSetupGameBursted)
        {
            var config = kvp.Key;
            var setupGame = kvp.Value;

            if (config.ScriptingBackend == ScriptingBackend.TinyIl2cpp)
            {
                setupGame = Il2Cpp.UnityLinker.SetupInvocation(setupGame, $"artifacts/{game.Name}/{config.Identifier}_stripped", config.NativeProgramConfiguration);
                il2CppOutputProgram.SetupConditionalSourcesAndLibrariesForConfig(config, setupGame);
                configToSetupGameStripped[kvp.Key] = setupGame;
            }
            else
            {
                configToSetupGameStripped[kvp.Key] = kvp.Value;
            }
        }

        foreach (var kvp in configToSetupGameStripped)
        {
            var config = kvp.Key;
            var setupGame = kvp.Value;
            NPath deployPath = GameDeployDirectoryFor(gameProgram, config);
            
            IDeployable deployedGame;
            NPath entryPointExecutable = null;

            if (config.ScriptingBackend == ScriptingBackend.TinyIl2cpp)
            {
                var builtNativeProgram = il2CppOutputProgram.SetupSpecificConfiguration(
                        config.NativeProgramConfiguration,
                        config.NativeProgramConfiguration.ExecutableFormat
                        )
                        .WithDeployables(setupGame.RecursiveRuntimeDependenciesIncludingSelf.SelectMany(a => a.Deployables.Where(d=>!(d is DotNetAssembly) && !(d is StaticLibrary)))
                        .ToArray());

                if (builtNativeProgram is IPackagedAppExtension)
                {
                    (builtNativeProgram as IPackagedAppExtension).SetAppPackagingParameters(
                        gameProgram.AsmDefDescription.Name,
                        config.DotsConfiguration,
                        gameProgram.SupportFiles.For(config).Concat(il2CppOutputProgram.SupportFiles.For(config.NativeProgramConfiguration))
                        );
                }
                deployedGame = builtNativeProgram.DeployTo(deployPath);
                entryPointExecutable = deployedGame.Path;
                if (config.EnableManagedDebugging && !(builtNativeProgram is IPackagedAppExtension))
                    Backend.Current.AddDependency(deployedGame.Path, Il2Cpp.CopyIL2CPPMetadataFile(deployPath, setupGame));
            }
            else
            {
                deployedGame  = setupGame.DeployTo(deployPath);

                var dotNetAssembly = (DotNetAssembly) deployedGame;
                
                //Usually a dotnet runtime game does not have a static void main(), and instead references another "entrypoint asmdef" that provides it.
                //This is convenient, but what makes it weird is that you have to start YourEntryPoint.exe  instead of YourGame.exe.   Until we have a better
                //solution for this, we're going to copy YourEntryPoint.exe to YourGame.exe, so that it's easier to find, and so that when it runs and you look
                //at the process name you understand what it is.
                if (deployedGame.Path.HasExtension("dll"))
                {
                    var to = deployPath.Combine(deployedGame.Path.ChangeExtension("exe").FileName);
                    var from = dotNetAssembly.RecursiveRuntimeDependenciesIncludingSelf.SingleOrDefault(a=>a.Path.HasExtension("exe"))?.Path;
                    if (from == null)
                        throw new InvalidProgramException($"Program {dotNetAssembly.Path} is an executable-like thing, but doesn't reference anything with Main");
                    Backend.Current.AddDependency(deployedGame.Path, CopyTool.Instance().Setup(to, from));
                    entryPointExecutable = to;
                }
                else
                {
                    entryPointExecutable = deployedGame.Path;
                }
            }

            //Because we use multidag, and try to not run all the setupcode when we just want to create projectfiles, we have a bit of a challenge.
            //Projectfiles require exact start and build commands. So we need to have a cheap way to calculate those. However, it's important that they
            //exactly match the actual place where the buildprogram is going to place our files. If these don't match things break down. The checks
            //in this block, they compare the "quick way to determine where the binary will be placed, and what the start executable is",  with the
            //actual return values returned from .DeployTo(), when we do run the actual buildcode.
            NPath deployedGamePath = GameDeployBinaryFor(gameProgram, config);
            if (deployedGame.Path != deployedGamePath)
                throw new InvalidProgramException($"We expected deployPath to be {deployedGamePath}, but in reality it was {deployedGame.Path}");
            var expectedEntryPointExecutable = EntryPointExecutableFor(gameProgram, config);
            if (entryPointExecutable != expectedEntryPointExecutable)
                throw new InvalidProgramException($"We expected entryPointExecutable to be {expectedEntryPointExecutable}, but in reality it was {entryPointExecutable}");

            Backend.Current.AddAliasDependency(config.Identifier, deployedGamePath);
        }

        return gameProgram;
    }

    private static NPath EntryPointExecutableFor(AsmDefCSharpProgram gameProgram, DotsRuntimeCSharpProgramConfiguration config)
    {
        if (gameProgram.FileName.EndsWith(".exe") || config.ScriptingBackend != ScriptingBackend.Dotnet)
            return GameDeployBinaryFor(gameProgram,config);
       
        return GameDeployDirectoryFor(gameProgram, config).Combine(new NPath(gameProgram.FileName).FileNameWithoutExtension+".exe");
    }

    private static NPath GameDeployBinaryFor(AsmDefCSharpProgram game, DotsRuntimeCSharpProgramConfiguration config)
    {
        var ext = config.NativeProgramConfiguration.ExecutableFormat.Extension;
        if (!ext.StartsWith(".") && !String.IsNullOrEmpty(ext))
            ext = "." + ext;
        var fileName = config.ScriptingBackend == ScriptingBackend.Dotnet ? 
            game.FileName
            : new NPath(game.AsmDefDescription.Name) + ext;
        
        return GameDeployDirectoryFor(game, config).Combine(fileName);
    }

    private static NPath GameDeployDirectoryFor(AsmDefCSharpProgram game, DotsRuntimeCSharpProgramConfiguration config)
    {
        if (config.FinalOutputDirectory != null)
            if(config.FinalOutputDirectory.IsRelative)
                return new NPath("../..").Combine(config.FinalOutputDirectory);
            else
                return config.FinalOutputDirectory;
        else
            return $"../../Builds/{config.Identifier}";
    }

    private static StartInfo StartInfoFor(DotsRuntimeCSharpProgramConfiguration config, NPath deployedGamePath)
    {
        if (config.Platform is WebGLPlatform)
            return new BrowserStartInfo(new Uri(deployedGamePath.MakeAbsolute().ToString(SlashMode.Native)).AbsoluteUri);
        
        return new ExecutableStartInfo(new Shell.ExecuteArgs() {Executable = deployedGamePath, WorkingDirectory = deployedGamePath.Parent }, true);
    }

    static readonly Cache<AsmDefCSharpProgram, AsmDefDescription> _cache = new Cache<AsmDefCSharpProgram, AsmDefDescription>();

    public static AsmDefCSharpProgram GetOrMakeDotsRuntimeCSharpProgramFor(
        AsmDefDescription asmDefDescription) =>
        _cache.GetOrMake(asmDefDescription, () => new AsmDefCSharpProgram(asmDefDescription));

}
