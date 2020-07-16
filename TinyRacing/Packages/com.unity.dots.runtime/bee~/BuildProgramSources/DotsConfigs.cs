using DotsBuildTargets;
using Newtonsoft.Json.Linq;
using NiceIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bee.Core;
using Unity.BuildSystem.CSharpSupport;
using Unity.BuildSystem.NativeProgramSupport;

public static class DotsConfigs
{
    private static Dictionary<string, List<DotsRuntimeCSharpProgramConfiguration>> PerConfigBuildSettings =
        new Dictionary<string, List<DotsRuntimeCSharpProgramConfiguration>>();

    public static Dictionary<string, List<DotsRuntimeCSharpProgramConfiguration>> MakeConfigs()
    {
        var platformList = DotsBuildSystemTargets;

        var settingsDir = new NPath("settings");

        if (settingsDir.Exists())
        {
            foreach (var settingsRelative in settingsDir.Files("*.json"))
            {
                var settingsFile = settingsRelative.MakeAbsolute();
                if (settingsFile.Exists())
                {
                    Backend.Current.RegisterFileInfluencingGraph(settingsFile);
                    var settingsObject = new FriendlyJObject {Content = JObject.Parse(settingsFile.ReadAllText())};

                    if(settingsObject.GetInt("Version", 0) != AsmDefConfigFile.BuildSettingsFileVersion)
                    {
                        Console.WriteLine($"Found old settings file '{settingsFile}', removing...");
                        settingsFile.Delete();
                        continue;
                    }

                    var id = settingsObject.GetString("PlatformTargetIdentifier");

                    var target = platformList.Single(t => t.Identifier == id);

                    if (!target.ToolChain.CanBuild)
                        continue;

                    var multithreading = settingsObject.GetBool("EnableMultithreading");
                    var targetShouldUseBurst = settingsObject.GetBool("EnableBurst");
                    if (!targetShouldUseBurst && multithreading)
                    {
                        Console.WriteLine($"Warning: BuildConfiguration '{settingsFile.FileNameWithoutExtension}' " +
                            $"specified 'EnableBurst=False', but 'Multithreading=True'. Multithreading requires Burst, therefore enabling Burst.");
                        targetShouldUseBurst = true;
                    }

                    var enableProfiler = ShouldEnableDevelopmentOptionForSetting("EnableProfiler", new [] {DotsConfiguration.Develop}, settingsObject);

                    var dotsCfg = DotsConfigForSettings(settingsObject, out var codegen);
                    var enableUnityCollectionsChecks = ShouldEnableDevelopmentOptionForSetting("EnableSafetyChecks",
                        new[] {DotsConfiguration.Debug, DotsConfiguration.Develop}, settingsObject);

                    if (!target.CanUseBurst && targetShouldUseBurst)
                    {
                        Console.WriteLine($"Warning: BuildConfiguration '{settingsFile.FileNameWithoutExtension}' " +
                            $"specified 'EnableBurst', but target ({target.Identifier}) does not support burst yet. Not using burst.");
                        targetShouldUseBurst = false;
                    }

                    var mdb = ShouldEnableDevelopmentOptionForSetting("EnableManagedDebugging",
                        new[] {DotsConfiguration.Debug}, settingsObject);

                    if (target.Identifier == "asmjs" || target.Identifier == "wasm")
                        mdb = false;
                    var waitForManagedDebugger = settingsObject.GetBool("WaitForManagedDebugger");

                    var rootAssembly = settingsObject.GetString("RootAssembly");
                    string finalOutputDir = null;
                    if (settingsObject.Content.TryGetValue("FinalOutputDirectory", out var finalOutputToken))
                        finalOutputDir = finalOutputToken.Value<string>();

                    var defines = new List<string>();
                    if (settingsObject.Content.TryGetValue("ScriptingDefines", out var definesJToken))
                        defines = ((JArray) definesJToken).Select(token => token.Value<string>()).ToList();

                    if (!PerConfigBuildSettings.ContainsKey(rootAssembly))
                        PerConfigBuildSettings[rootAssembly] = new List<DotsRuntimeCSharpProgramConfiguration>();

                    PerConfigBuildSettings[rootAssembly]
                        .Add(
                            new DotsRuntimeCSharpProgramConfiguration(
                                csharpCodegen: codegen,
                                cppCodegen: codegen == CSharpCodeGen.Debug ? CodeGen.Debug : CodeGen.Release,
                                nativeToolchain: target.ToolChain,
                                scriptingBackend: target.ScriptingBackend,
                                targetFramework: target.TargetFramework,
                                identifier: settingsFile.FileNameWithoutExtension,
                                enableUnityCollectionsChecks: enableUnityCollectionsChecks,
                                enableManagedDebugging: mdb,
                                waitForManagedDebugger: waitForManagedDebugger,
                                multiThreadedJobs: multithreading,
                                dotsConfiguration: dotsCfg,
                                enableProfiler: enableProfiler,
                                useBurst: targetShouldUseBurst,
                                executableFormat: target.CustomizeExecutableForSettings(settingsObject),
                                defines: defines,
                                finalOutputDirectory: finalOutputDir));
                }
            }
        }

        return PerConfigBuildSettings;
    }

    public static DotsConfiguration DotsConfigForSettings(FriendlyJObject settingsObject, out CSharpCodeGen codegen)
    {
        DotsConfiguration dotsCfg;
        var codegenString = settingsObject.GetString("DotsConfig");
        switch (codegenString)
        {
            case "Debug":
                codegen = CSharpCodeGen.Debug;
                dotsCfg = DotsConfiguration.Debug;
                break;
            case "Develop":
                codegen = CSharpCodeGen.Release;
                dotsCfg = DotsConfiguration.Develop;
                break;
            case "Release":
                codegen = CSharpCodeGen.Release;
                dotsCfg = DotsConfiguration.Release;
                break;
            default:
                throw new ArgumentException(
                    $"Error: Unrecognized codegen {codegenString} in build json file. This is a bug.");
        }

        return dotsCfg;
    }

    public static bool ShouldEnableDevelopmentOptionForSetting(string optionName, DotsConfiguration[] enabledByDefaultForConfigurations, FriendlyJObject settingsObject)
    {
        var optionString = settingsObject.GetString(optionName);
        if (string.IsNullOrEmpty(optionString) || optionString == "UseBuildConfiguration")
        {
            var dotsConfig = DotsConfigForSettings(settingsObject, out var unused);
            return enabledByDefaultForConfigurations.Contains(dotsConfig);
        }
        if (optionString == "Enabled")
            return true;
        if (optionString == "Disabled")
            return false;
        throw new ArgumentException($"Error: Unrecognized '{optionName}' option '{optionString}' in build json file. This is a bug.");
    }

    private static List<DotsBuildSystemTarget> _dotsBuildSystemTargets;

    private static List<DotsBuildSystemTarget> DotsBuildSystemTargets
    {
        get
        {
            if (_dotsBuildSystemTargets != null)
                return _dotsBuildSystemTargets;

            var platformList = new List<DotsBuildSystemTarget>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;

                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }

                foreach (var type in types)
                {
                    if (type.IsAbstract)
                        continue;

                    if (!type.IsSubclassOf(typeof(DotsBuildSystemTarget)))
                        continue;

                    platformList.Add((DotsBuildSystemTarget) Activator.CreateInstance(type));
                }
            }

            _dotsBuildSystemTargets = platformList;

            return _dotsBuildSystemTargets;
        }
    }

    private static Lazy<DotsRuntimeCSharpProgramConfiguration> _multiThreadedJobsTestConfig =
        new Lazy<DotsRuntimeCSharpProgramConfiguration>(() =>
            HostDotnet.WithMultiThreadedJobs(true).WithIdentifier(HostDotnet.Identifier + "-mt"));

    public static DotsRuntimeCSharpProgramConfiguration MultithreadedJobsTestConfig =>
        _multiThreadedJobsTestConfig.Value;

    public static DotsRuntimeCSharpProgramConfiguration HostDotnet
    {
        get
        {
            var target = DotsBuildSystemTargets.First(c =>
                c.ScriptingBackend == ScriptingBackend.Dotnet &&
                c.ToolChain.Platform.GetType() == Platform.HostPlatform.GetType());
            return new DotsRuntimeCSharpProgramConfiguration(
                csharpCodegen: CSharpCodeGen.Release,
                cppCodegen: CodeGen.Release,
                nativeToolchain: target.ToolChain,
                scriptingBackend: ScriptingBackend.Dotnet,
                targetFramework: TargetFramework.Tiny,
                identifier: "HostDotNet",
                enableUnityCollectionsChecks: true,
                enableManagedDebugging: false,
                waitForManagedDebugger: false,
                multiThreadedJobs: false,
                dotsConfiguration: DotsConfiguration.Develop,
                enableProfiler: false,
                useBurst: true);
        }
    }
}
