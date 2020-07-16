using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Bee;
using Bee.Core;
using Bee.CSharpSupport;
using Bee.DotNet;
using Bee.NativeProgramSupport.Building;
using Bee.Toolchain.Emscripten;
using Bee.Toolchain.Xcode;
using Bee.VisualStudioSolution;
using NiceIO;
using Unity.BuildSystem.CSharpSupport;
using Unity.BuildSystem.NativeProgramSupport;
using Unity.BuildTools;

public enum DotsConfiguration
{
    Debug,
    Develop,
    Release,
}

/// <summary>
/// DotsRuntimeCSharpProgram is a csharp program that targets dots-runtime. It follows a particular file structure. It always has a folder
/// that folder can have *.cs files, which will be part of the csharp program. The folder can also have a .cpp~ and .js~ folder.  If any
/// of those are present, DotsRuntimeCSharpProgram will build a NativeProgram with those .cpp files and .js libraries side by side. The common
/// usecase for this is for the c# code to [DllImport] pinvoke into the c++ code.
///
/// A DotsRuntimeCSharpProgram does not know about asmdefs (e.g. Unity.LowLevel)
/// </summary>
public class DotsRuntimeCSharpProgram : CSharpProgram
{
    private bool _doneEnsureNativeProgramLinksToReferences;
    
    public NPath MainSourcePath { get; }
    public List<NPath> ExtraSourcePaths { get; }
    private IEnumerable<NPath> AllSourcePaths => new[] {MainSourcePath}.Concat(ExtraSourcePaths);
    public NativeProgram NativeProgram { get; set; }
    public Platform[] IncludePlatforms { get; set; }
    public Platform[] ExcludePlatforms { get; set; }

    // Unilaterally enable warnings as errors for any assembly which matches one of the following patterns
    static readonly string[] EnableWarningsAsErrorsPatterns = 
    {
        @"Unity\..*" // Force Warnings as Errors for all Unity asmdefs
    };

    // Explicitly prevent enabling warnings as errors for any assembly matched by this list
    static readonly string[] EnableWarningsAsErrorsExclusionsRegex =
    {
        @"\.Tests$", // Exclude all tests from having Warnings as Errors
        @"Unity.Physics" // Currently generates warnings
    };

    public virtual bool IsSupportedFor(CSharpProgramConfiguration config)
    {
        if (config is DotsRuntimeCSharpProgramConfiguration dotsConfig)
        {
            if (IncludePlatforms?.Any(p => p.GetType().IsInstanceOfType(dotsConfig.Platform)) ?? false)
                return true;

            if (IncludePlatforms?.Any() ?? false)
                return false;
            
            if (!ExcludePlatforms?.Any() ?? true)
                return true;

            return !ExcludePlatforms?.Any(p => p.GetType().IsInstanceOfType(dotsConfig.Platform)) ?? true;
        }

        return false;
    }

    public DotsRuntimeCSharpProgram(NPath mainSourcePath,
        IEnumerable<NPath> extraSourcePaths = null,
        string name = null,
        bool isExe = false,
        bool deferConstruction = false)
    {
        MainSourcePath = mainSourcePath;
        ExtraSourcePaths = extraSourcePaths?.ToList() ?? new List<NPath>();
        name = name ?? MainSourcePath.FileName;
        
        if (!deferConstruction)
            Construct(name, isExe);

        //ProjectFile.ExplicitConfigurationsToUse = new CSharpProgramConfiguration[] {DotsConfigs.ProjectFileConfig};

        ProjectFile.IntermediateOutputPath.Set(config => Configuration.RootArtifactsPath.Combine(ArtifactsGroup ?? "Bee.CSharpSupport").Combine("MSBuildIntermediateOutputPath", config.Identifier));
    }

    protected void Construct(string name, bool isExe)
    {
        FileName = name + (isExe ? ".exe" : ".dll");

        Framework.Add(c => GetTargetFramework(c, this) == TargetFramework.Tiny, Bee.DotNet.Framework.FrameworkNone);
        References.Add(c => GetTargetFramework(c, this) == TargetFramework.Tiny, Il2Cpp.TinyCorlib);

        Framework.Add(
            c => GetTargetFramework(c, this) == TargetFramework.NetStandard20,
            BuildProgram.HackedFrameworkToUseForProjectFilesIfNecessary);
        
        ProjectFile.Path = DeterminePathForProjectFile();

        ProjectFile.ReferenceModeCallback = arg =>
        {
            // Most projects are AsmDefCSharpProgram. For everything else we'll look up their
            // packagestatus by the fact that we know it's in the same package as Unity.Entities.CPlusPlus
            // XXX This is not true any more!
            //var asmdefDotsProgram = (arg as AsmDefCSharpProgram)?.AsmDefDescription ?? AsmDefConfigFile.AsmDefDescriptionFor("Unity.Entities.CPlusPlus");
            return ProjectFile.ReferenceMode.ByCSProj;
        };

        LanguageVersion = "7.3";
        Defines.Add(
            "UNITY_DOTSPLAYER",
            "NET_DOTS",
            "NET_STANDARD_2_0",
            "UNITY_2018_3_OR_NEWER",
            "UNITY_2019_1_OR_NEWER",
            "UNITY_2019_2_OR_NEWER",
            "UNITY_2019_3_OR_NEWER"
        );

        Defines.Add(c => (c as DotsRuntimeCSharpProgramConfiguration)?.NativeProgramConfiguration?.ToolChain.Architecture.Bits == 32, "UNITY_DOTSPLAYER32");
        Defines.Add(c => (c as DotsRuntimeCSharpProgramConfiguration)?.NativeProgramConfiguration?.ToolChain.Architecture.Bits == 64, "UNITY_DOTSPLAYER64");

        Defines.Add(c => (c as DotsRuntimeCSharpProgramConfiguration)?.Platform is WebGLPlatform, "UNITY_WEBGL");
        Defines.Add(c =>(c as DotsRuntimeCSharpProgramConfiguration)?.Platform is WindowsPlatform, "UNITY_WINDOWS");
        Defines.Add(c =>(c as DotsRuntimeCSharpProgramConfiguration)?.Platform is MacOSXPlatform, "UNITY_MACOSX");
        Defines.Add(c => (c as DotsRuntimeCSharpProgramConfiguration)?.Platform is LinuxPlatform, "UNITY_LINUX");
        Defines.Add(c =>(c as DotsRuntimeCSharpProgramConfiguration)?.Platform is IosPlatform, "UNITY_IOS");
        Defines.Add(c => (c as DotsRuntimeCSharpProgramConfiguration)?.Platform is AndroidPlatform, "UNITY_ANDROID");
        Defines.Add(c => !((DotsRuntimeCSharpProgramConfiguration) c).MultiThreadedJobs, "UNITY_SINGLETHREADED_JOBS");

        CopyReferencesNextToTarget = false;

        SetWarningsAsErrors(name);

        foreach (var sourcePath in AllSourcePaths)
        {
            if (sourcePath.FileName == "Unity.Mathematics")
                Sources.Add(sourcePath.Files("*.cs", true)
                    .Where(f => f.FileName != "math_unity_conversion.cs" && f.FileName != "PropertyAttributes.cs"));
            else
            {
                Sources.Add(new CustomProvideFiles(sourcePath));
            }
        }
        bool hasCpp = false;
        foreach (var sourcePath in AllSourcePaths)
        {
            var cppFolder = sourcePath.Combine("cpp~");
            var androidFolder = sourcePath.Combine("android~");
            var prejsFolder = sourcePath.Combine("prejs~");
            var jsFolder = sourcePath.Combine("js~");
            var postjsFolder = sourcePath.Combine("postjs~");
            var beeFolder = sourcePath.Combine("bee~");
            var includeFolder = cppFolder.Combine("include");
            var bindingsFolder = sourcePath.Combine("bindings~");

            if (cppFolder.DirectoryExists())
            {
                ProjectFile.AdditionalFiles.AddRange(cppFolder.Files(true));

                var cppFiles = cppFolder.Files("*.c*", true);
                GetOrMakeNativeProgram().Sources.Add(cppFiles);

                var mmFiles = cppFolder.Files("*.m*", true);
                GetOrMakeNativeProgram().Sources.Add(c => (c.Platform is MacOSXPlatform || c.Platform is IosPlatform), mmFiles);

                GetOrMakeNativeProgram().DynamicLinkerSettingsForAndroid().Add(c => ((DotsRuntimeNativeProgramConfiguration)c).CSharpConfig.DotsConfiguration == DotsConfiguration.Release, l => l.WithStripAll(true));

                hasCpp = true;
            }

            if (prejsFolder.DirectoryExists())
            {
                var jsFiles = prejsFolder.Files("*.js", true);
                ProjectFile.AdditionalFiles.AddRange(prejsFolder.Files(true));
                GetOrMakeNativeProgram()
                    .Libraries.Add(c => c.Platform is WebGLPlatform,
                        jsFiles.Select(jsFile => new PreJsLibrary(jsFile)));
            }

            //todo: get rid of having both a regular js and a prejs folder
            if (jsFolder.DirectoryExists())
            {
                var jsFiles = jsFolder.Files("*.js", true);
                ProjectFile.AdditionalFiles.AddRange(jsFolder.Files(true));
                GetOrMakeNativeProgram()
                    .Libraries.Add(c => c.Platform is WebGLPlatform,
                        jsFiles.Select(jsFile => new JavascriptLibrary(jsFile)));
            }

            if (postjsFolder.DirectoryExists())
            {
                var jsFiles = postjsFolder.Files("*.js", true);
                ProjectFile.AdditionalFiles.AddRange(postjsFolder.Files(true));
                GetOrMakeNativeProgram()
                    .Libraries.Add(c => c.Platform is WebGLPlatform,
                        jsFiles.Select(jsFile => new PostJsLibrary(jsFile)));
            }

            if (beeFolder.DirectoryExists())
                ProjectFile.AdditionalFiles.AddRange(beeFolder.Files("*.cs"));

            if (includeFolder.DirectoryExists())
                GetOrMakeNativeProgram().PublicIncludeDirectories.Add(includeFolder);

            if (bindingsFolder.DirectoryExists())
            {
                NativeJobsPrebuiltLibrary.AddBindings(this, bindingsFolder);
            }

            if (androidFolder.DirectoryExists())
            {
                foreach (var extraFile in androidFolder.Files(true).Where(f => f.HasExtension("java", "kt", "aar", "jar")))
                {
                    SupportFiles.Add(c => (c as DotsRuntimeCSharpProgramConfiguration)?.Platform is AndroidPlatform, new DeployableFile(extraFile, extraFile.RelativeTo(androidFolder)));
                }
            }
        }

        if (hasCpp)
        {
            GetOrMakeNativeProgram().Libraries.Add(c => c.Platform is LinuxPlatform, new SystemLibrary("rt"));
            GetOrMakeNativeProgram().Libraries.Add(c => c.Platform is WindowsPlatform, new SystemLibrary("ws2_32.lib"));
            NativeJobsPrebuiltLibrary.AddToNativeProgram(GetOrMakeNativeProgram());
        }

        SupportFiles.Add(AllSourcePaths.SelectMany(p =>
            p.Files()
                .Where(f => f.HasExtension("jpg", "png", "wav", "mp3", "jpeg", "mp4", "webm", "ogg", "ttf", "json"))));

        Defines.Add(c => ((DotsRuntimeCSharpProgramConfiguration)c).EnableUnityCollectionsChecks, "ENABLE_UNITY_COLLECTIONS_CHECKS");

        bool isConfigDebug(CSharpProgramConfiguration c) =>
            c.CodeGen == CSharpCodeGen.Debug || (c as DotsRuntimeCSharpProgramConfiguration)?.DotsConfiguration < DotsConfiguration.Release;
        Defines.Add(isConfigDebug, "DEBUG");

        bool isConfigProfilerEnabled(CSharpProgramConfiguration c) => ((DotsRuntimeCSharpProgramConfiguration)c).EnableProfiler;

        // The profiler + managed debugging with IL2CPP makes the player really slow. If both are enabled, managed
        // debugging should be enabled, but the profiler will not be enabled.
        Defines.Add(c =>
        {
            if (IsManagedDebuggingWithIL2CPPEnabled(c))
            {
                Errors.PrintWarning("The profiler cannot be enabled with managed debugging and IL2CPP. The profiler will be disabled for this build.");
                return false;
            }
            return isConfigProfilerEnabled(c);
        }, "ENABLE_PROFILER");

        // Only enable player connection when we need it
        // - To support logging ("debug" builds)
        // - To support profiling
        // - To support il2cpp managed debugging (multicast)
        Defines.Add(c => isConfigDebug(c) || isConfigProfilerEnabled(c) || IsManagedDebuggingWithIL2CPPEnabled(c), "ENABLE_PLAYERCONNECTION");

        // Multicasting
        // - Is a supplement to player connection in non-webgl builds
        // - Is needed for identification in webgl builds, too, if il2cpp managed debugging is enabled
        Defines.Add(c => !((c as DotsRuntimeCSharpProgramConfiguration).Platform is WebGLPlatform) || IsManagedDebuggingWithIL2CPPEnabled(c), "ENABLE_MULTICAST");

        Defines.Add(
            c => (c as DotsRuntimeCSharpProgramConfiguration)?.ScriptingBackend == ScriptingBackend.TinyIl2cpp,
            "UNITY_DOTSPLAYER_IL2CPP");
        Defines.Add(c => IsManagedDebuggingWithIL2CPPEnabled(c), "UNITY_DOTSPLAYER_IL2CPP_MANAGED_DEBUGGER");
        Defines.Add(c => IsManagedDebuggingWithIL2CPPEnabled(c) && (c as DotsRuntimeCSharpProgramConfiguration).WaitForManagedDebugger,
            "UNITY_DOTSPLAYER_IL2CPP_WAIT_FOR_MANAGED_DEBUGGER");
        Defines.Add(c => (c as DotsRuntimeCSharpProgramConfiguration)?.ScriptingBackend == ScriptingBackend.Dotnet, "UNITY_DOTSPLAYER_DOTNET");
        Defines.Add(c => (c as DotsRuntimeCSharpProgramConfiguration)?.Defines ?? new List<string>());

        ProjectFile.RedirectMSBuildBuildTargetToBee = true;
        ProjectFile.AddCustomLinkRoot(MainSourcePath, ".");
        ProjectFile.RootNameSpace = "";

        DotsRuntimeCSharpProgramCustomizer.RunAllCustomizersOn(this);
    }

    private static bool IsManagedDebuggingWithIL2CPPEnabled(CSharpProgramConfiguration c)
    {
        return (c as DotsRuntimeCSharpProgramConfiguration).EnableManagedDebugging &&
               (c as DotsRuntimeCSharpProgramConfiguration).ScriptingBackend == ScriptingBackend.TinyIl2cpp;
    }

    protected virtual NPath DeterminePathForProjectFile()
    {
        return new NPath(FileName).ChangeExtension(".csproj");
    }

    public static bool DoesPackageSourceIndicateUserHasControlOverSource(string packageSource)
    {
        switch (packageSource)
        {
            case "NoPackage":
            case "Local":
            case "Embedded":
                return true;
            default:
                return false;
        }
    }

    internal NativeProgram GetOrMakeNativeProgram()
    {
        if (NativeProgram != null)
            return NativeProgram;

        var libname = "lib_" + new NPath(FileName).FileNameWithoutExtension.ToLower().Replace(".", "_");
        NativeProgram = new NativeProgram(libname);
        
        SetupDotsRuntimeNativeProgram(libname, NativeProgram);
        // sigh
        NativeProgram.Defines.Add("BUILD_" + MainSourcePath.FileName.ToUpper().Replace(".", "_") + "=1");
        
        return NativeProgram;
    }

    public static void SetupDotsRuntimeNativeProgram(string libname, NativeProgram np)
    {
        np.DynamicLinkerSettingsForMac().Add(c => c.WithInstallName(libname + ".dylib"));
        np.DynamicLinkerSettingsForIos()
            .Add(c => c.WithInstallName("@executable_path/Frameworks/" + libname + ".dylib"));
        np.IncludeDirectories.Add(BuildProgram.BeeRootValue.Combine("cppsupport/include"));

        //lets always add a dummy cpp file, in case this np is only used to carry other libraries
        np.Sources.Add(BuildProgram.BeeRootValue.Combine("cppsupport/dummy.cpp"));

        np.Defines.Add(c => c.Platform is WebGLPlatform, "UNITY_WEBGL=1");
        np.Defines.Add(c => c.Platform is WindowsPlatform, "UNITY_WINDOWS=1");
        np.Defines.Add(c => c.Platform is MacOSXPlatform, "UNITY_MACOSX=1");
        np.Defines.Add(c => c.Platform is LinuxPlatform, "UNITY_LINUX=1");
        np.Defines.Add(c => c.Platform is IosPlatform, "UNITY_IOS=1");
        np.Defines.Add(c => c.Platform is AndroidPlatform, "UNITY_ANDROID=1");
        np.Defines.Add(c => c.CodeGen == CodeGen.Debug, "DEBUG=1");

        np.Defines.Add("BINDGEM_DOTS=1");
        np.Defines.Add(c =>
                ((DotsRuntimeNativeProgramConfiguration) c).CSharpConfig.EnableManagedDebugging &&
                ((DotsRuntimeNativeProgramConfiguration) c).CSharpConfig.WaitForManagedDebugger,
            "UNITY_DOTSPLAYER_IL2CPP_WAIT_FOR_MANAGED_DEBUGGER");

        //we don't want to do this for c#, because then burst sees different code from the unbursted path and it's very
        //easy and tempting to go insane this way. but for native, it's fine, since burst
        //doesn't see that directly. and also, it enables us to error when we don't find the burst dll when burst is on, 
        //and not look for it when it's off. 
        np.Defines.Add(c => ((DotsRuntimeNativeProgramConfiguration) c).CSharpConfig.UseBurst, "ENABLE_UNITY_BURST=1");
    }
    
    protected virtual TargetFramework GetTargetFramework(CSharpProgramConfiguration config, DotsRuntimeCSharpProgram program)
    {
        if (config is DotsRuntimeCSharpProgramConfiguration dotsConfig)
            return dotsConfig.TargetFramework;

        return TargetFramework.Tiny;
    }

    public override DotNetAssembly SetupSpecificConfiguration(CSharpProgramConfiguration config)
    {
        EnsureNativeProgramLinksToReferences();
        
        var result = base.SetupSpecificConfiguration(config);

        return SetupNativeProgram(config, result);
    }

    protected virtual DotNetAssembly SetupNativeProgram(CSharpProgramConfiguration config, DotNetAssembly result)
    {
        var dotsConfig = (DotsRuntimeCSharpProgramConfiguration) config;

        var npc = dotsConfig.NativeProgramConfiguration;
        if (NativeProgram != null && NativeProgram.Sources.ForAny().Any() && npc != null)
        {
            BuiltNativeProgram setupSpecificConfiguration = NativeProgram.SetupSpecificConfiguration(npc,
                npc.ToolChain.DynamicLibraryFormat ?? npc.ToolChain.StaticLibraryFormat);
            result = result.WithDeployables(setupSpecificConfiguration);
        }

        return result;
    }

    private void EnsureNativeProgramLinksToReferences()
    {
        //todo: find a more elegant way than this..
        if (_doneEnsureNativeProgramLinksToReferences)
            return;
        _doneEnsureNativeProgramLinksToReferences = true;
        
        NativeProgram?.Libraries.Add(npc =>
        {
            var csharpConfig = ((DotsRuntimeNativeProgramConfiguration) npc).CSharpConfig;
            var dotsRuntimeCSharpPrograms = References.For(csharpConfig)
                .OfType<DotsRuntimeCSharpProgram>()
                .Where(p => p.IsSupportedFor(csharpConfig))
                .ToArray();
            return dotsRuntimeCSharpPrograms.Select(dcp => dcp.NativeProgram).Where(np => np != null)
                .Select(np => new NativeProgramAsLibrary(np) { BuildMode = NativeProgramLibraryBuildMode.Dynamic});
        });
    }

    protected class CustomProvideFiles : OneOrMoreFiles
    {
        public NPath SourcePath { get; }

        public CustomProvideFiles(NPath sourcePath) => SourcePath = sourcePath;

        public override IEnumerable<NPath> GetFiles()
        {
            var files = SourcePath.Files("*.cs", recurse:true);
            var ignoreDirectories = FindDirectories();
            return files.Where(f => f.HasExtension("cs") && !ignoreDirectories.Any(f.IsChildOf));
        }

        private List<NPath> FindDirectories()
        {
            var skippedDirs = SourcePath.Directories(true).Where(ShouldSkipName).ToList();
            var ignoreDirectories = SourcePath.Files("*.asm?ef", recurse: true)
                .Where(f => f.Parent != SourcePath)
                .Select(asmdef => asmdef.Parent)
                .Concat(skippedDirs)
                .ToList();
            return ignoreDirectories;
        }

        private bool ShouldSkipName(NPath item)
        {
            var filename = item.FileName;
            return filename[filename.Length - 1] == '~';
        }

        public override IEnumerable<NPath> GetDirectories()
        {
            return SourcePath.Directories(true).Where(d => ShouldSkipName(d) && !FindDirectories().Any(d.IsChildOf));
        }

        public override IEnumerable<XElement> CustomMSBuildElements(NPath projectFileParentPath)
        {
            if (SourcePath != projectFileParentPath && !SourcePath.IsChildOf(projectFileParentPath)) 
                return null;

            var relative = SourcePath.RelativeTo(projectFileParentPath).ToString(SlashMode.Native);

            var prefix = relative == "." ? "" : $"{relative}\\";
            var ns = ProjectFileContentBuilder.DefaultNamespace;
            return new[]
            {
                new XElement(ns + "Compile", new XAttribute("Include", $@"{prefix}**\*.cs"),
                    new XAttribute("Exclude", $"{prefix}bee?\\**\\*.*"))
            };

        }
    }

    void SetWarningsAsErrors(string assemblyName)
    {
        WarningsAsErrors = false;

        foreach (var pattern in EnableWarningsAsErrorsExclusionsRegex)
        {
            var regex = new Regex(pattern);
            if (regex.IsMatch(assemblyName))
                return;
        }

        foreach (var pattern in EnableWarningsAsErrorsPatterns)
        {
            var regex = new Regex(pattern);
            if (regex.IsMatch(assemblyName))
            {
                WarningsAsErrors = true;
                return;
            }
        }
    }
}

public enum ScriptingBackend
{
    TinyIl2cpp,
    Dotnet
}

public enum TargetFramework
{
    Tiny,
    NetStandard20
}

public sealed class DotsRuntimeCSharpProgramConfiguration : CSharpProgramConfiguration
{
    public DotsRuntimeNativeProgramConfiguration NativeProgramConfiguration { get; set; }

    public ScriptingBackend ScriptingBackend { get; }

    public TargetFramework TargetFramework { get; }

    public DotsConfiguration DotsConfiguration { get; }

    private Platform _platform;
    public Platform Platform
    {
        get
        {
            return _platform ?? NativeProgramConfiguration.ToolChain.Platform;
        }
        set { _platform = value; }
    }
    
    public bool MultiThreadedJobs { get; private set; }
    
    public bool EnableProfiler { get; }

    public bool UseBurst { get; }

    private string _identifier { get; set; }

    public DotsRuntimeCSharpProgramConfiguration(
        CSharpCodeGen csharpCodegen,
        CodeGen cppCodegen,
        //The stevedore global manifest will override DownloadableCsc.Csc72 artifacts and use Csc73
        ToolChain nativeToolchain,
        ScriptingBackend scriptingBackend,
        TargetFramework targetFramework,
        string identifier,
        bool enableUnityCollectionsChecks,
        bool enableManagedDebugging,
        bool waitForManagedDebugger,
        bool multiThreadedJobs,
        DotsConfiguration dotsConfiguration,
        bool enableProfiler,
        bool useBurst,
        NativeProgramFormat executableFormat = null,
        IEnumerable<string> defines = null,
        NPath finalOutputDirectory = null)
        : base(
            csharpCodegen,
            DownloadableCsc.Csc72,
            DebugFormat.PortablePdb,
            nativeToolchain.Architecture is IntelArchitecture ? nativeToolchain.Architecture : null)
    {
        NativeProgramConfiguration = new DotsRuntimeNativeProgramConfiguration(
            cppCodegen,
            nativeToolchain,
            identifier,
            this,
            executableFormat: executableFormat);
        _identifier = identifier;
        EnableUnityCollectionsChecks = enableUnityCollectionsChecks;
        DotsConfiguration = dotsConfiguration;
        MultiThreadedJobs = multiThreadedJobs;
        EnableProfiler = enableProfiler;
        UseBurst = useBurst;
        EnableManagedDebugging = enableManagedDebugging;
        WaitForManagedDebugger = waitForManagedDebugger;
        ScriptingBackend = scriptingBackend;
        TargetFramework = targetFramework;
        Defines = defines?.ToList();
        FinalOutputDirectory = finalOutputDirectory;
    }


    public override string Identifier => _identifier;
    public bool EnableUnityCollectionsChecks { get; }
    public bool EnableManagedDebugging { get; }
    public bool WaitForManagedDebugger { get; }

    public DotsRuntimeCSharpProgramConfiguration WithMultiThreadedJobs(bool value) => MultiThreadedJobs == value ? this : With(c=>c.MultiThreadedJobs = value);
    public DotsRuntimeCSharpProgramConfiguration WithIdentifier(string value) => Identifier == value ? this : With(c=>c._identifier = value);

    private DotsRuntimeCSharpProgramConfiguration With(Action<DotsRuntimeCSharpProgramConfiguration> modifyCallback)
    {
        var copy = (DotsRuntimeCSharpProgramConfiguration) MemberwiseClone();
        modifyCallback(copy);
        return copy;
    }
    
    public List<string> Defines { get; set; }
    
    public NPath FinalOutputDirectory { get; set; }
}

public class DotsRuntimeNativeProgramConfiguration : NativeProgramConfiguration
{
    private NativeProgramFormat _executableFormat;
    public DotsRuntimeCSharpProgramConfiguration CSharpConfig { get; }
    public DotsRuntimeNativeProgramConfiguration(CodeGen codeGen, ToolChain toolChain, string identifier, DotsRuntimeCSharpProgramConfiguration cSharpConfig, NativeProgramFormat executableFormat = null) : base(codeGen, toolChain, false)
    {
        Identifier = identifier;
        CSharpConfig = cSharpConfig;
        _executableFormat = executableFormat;
    }

    public NativeProgramFormat ExecutableFormat => _executableFormat ?? base.ToolChain.ExecutableFormat;
    
    public override string Identifier { get; }
}
