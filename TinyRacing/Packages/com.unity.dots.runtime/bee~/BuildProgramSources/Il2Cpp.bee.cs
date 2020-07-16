using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using Bee;
using Bee.Core;
using Bee.CSharpSupport;
using Bee.DotNet;
using Bee.NativeProgramSupport.Building;
using Bee.NativeProgramSupport.Building.FluentSyntaxHelpers;
using Bee.Stevedore;
using Bee.Toolchain.Emscripten;
using Bee.Toolchain.GNU;
using Bee.Toolchain.LLVM;
using Bee.Toolchain.VisualStudio;
using Bee.Toolchain.Windows;
using Bee.Toolchain.Xcode;
using Newtonsoft.Json.Linq;
using NiceIO;
using Unity.BuildSystem.CSharpSupport;
using Unity.BuildSystem.NativeProgramSupport;
using Unity.BuildSystem.VisualStudio;
using Unity.BuildTools;

public static class Il2Cpp
{
    //change to tuple when we can finally use tuples
    struct DistributionAndDeps
    {
        public IFileBundle Distribution;
        public IFileBundle Deps;
    }

    static Lazy<DistributionAndDeps> _il2cppAndDeps = new Lazy<DistributionAndDeps>(() =>
    {
        NPath loc;
        if (Il2CppCustomLocation.CustomLocation != null && Environment.GetEnvironmentVariable("IL2CPP_FROM_STEVE") == null)
        {
            loc = Il2CppCustomLocation.CustomLocation;
            if (!loc.DirectoryExists())
                throw new ArgumentException(
                    $"Il2CppCustomLocation.CustomLocation set to {loc}, but that doesn't exist");

            var localDeps = loc.Parent.Combine("il2cpp-deps/artifacts/Stevedore");
            if (!localDeps.DirectoryExists())
                throw new ArgumentException(
                    "We found your il2cpp checkout, but not the il2cpp-deps directory next to it.");
            Console.WriteLine("Using il2cpp from local checkout");
            return new DistributionAndDeps()
                {Distribution = new LocalFileBundle(loc), Deps = new LocalFileBundle(localDeps)};
        }

        return new DistributionAndDeps() {Distribution = Il2CppFromSteve(), Deps = Il2CppDepsFromSteve()};
    });

    private const string metadataFilePath = "Data/Metadata/global-metadata.dat";
    
    public static IFileBundle Il2CppDependencies => _il2cppAndDeps.Value.Deps;
    public static IFileBundle Distribution => _il2cppAndDeps.Value.Distribution;

    private static DotNetAssembly _tinyCorlib => new DotNetAssembly(Distribution.Path.Combine("build/profiles/Tiny/mscorlib.dll"), Framework.FrameworkNone, referenceAssemblyPath:
        Distribution.Path.Combine("build/profiles/TinyStandard/netstandard.dll"));

    private static readonly DotNetRunnableProgram Il2CppRunnableProgram =
        new DotNetRunnableProgram(new DotNetAssembly(Distribution.Path.Combine("build/deploy/net471/il2cpp.exe"),
            Framework.Framework46));

    public static DotNetAssembly TinyCorlib => _tinyCorlib;

    private static IFileBundle Il2CppFromSteve()
    {
        var stevedoreArtifact = new StevedoreArtifact("il2cpp");
        Backend.Current.Register(stevedoreArtifact);
        return stevedoreArtifact;
    }

    private static IFileBundle Il2CppDepsFromSteve()
    {
        var stevedoreArtifact = new StevedoreArtifact("MonoBleedingEdgeSub");
        Backend.Current.Register(stevedoreArtifact);
        return stevedoreArtifact;
    }

    private static WarningAndPolicy[] GetGccLikeWarningPolicies()
    {
        return new[] {new WarningAndPolicy("invalid-offsetof", WarningPolicy.Silent)};
    }

    internal class Il2CppOutputProgram : NativeProgram
    {
        public Il2CppOutputProgram(string name) : base(name)
        {
            AddLibIl2CppAsLibraryFor(this);

            Libraries.Add(BoehmGCProgram);
            Sources.Add(Distribution.Path.Combine("external").Combine("xxHash/xxhash.c"));

            this.DynamicLinkerSettingsForMsvc()
                .Add(l => l.WithSubSystemType(SubSystemType.Console).WithEntryPoint("wWinMainCRTStartup"));

            Libraries.Add(c => c.ToolChain.Platform is WindowsPlatform, new SystemLibrary("kernel32.lib"));
            Defines.Add(c => c.Platform is WebGLPlatform, "IL2CPP_DISABLE_GC=1");

            this.DynamicLinkerSettingsForMsvc().Add(l => l
                .WithSubSystemType(SubSystemType.Console)
                .WithEntryPoint("wWinMainCRTStartup")
            );
            Defines.Add(c => c.ToolChain.DynamicLibraryFormat == null || c.Platform.Name == "IOS", "FORCE_PINVOKE_INTERNAL=1");

            this.DynamicLinkerSettingsForEmscripten().Add(c =>
                c.WithShellFile(BuildProgram.BeeRoot.Parent.Combine("LowLevelSupport~", "WebSupport", "tiny_shell.html")));

            this.DynamicLinkerSettingsForAndroid().Add(c =>
                ((DotsRuntimeNativeProgramConfiguration)c).CSharpConfig.DotsConfiguration == DotsConfiguration.Release, l => l.WithStripAll(true));

            Libraries.Add(c => c.Platform is WebGLPlatform,new PreJsLibrary(BuildProgram.BeeRoot.Parent.Combine("LowLevelSupport~", "WebSupport", "tiny_runtime.js")));
            Defines.Add(ManagedDebuggingIsEnabled, "IL2CPP_MONO_DEBUGGER=1");
            Defines.Add(ManagedDebuggingIsEnabled, "IL2CPP_DEBUGGER_PORT=56000");
            
            // Remove this comment to enable the managed debugger log file. It will be written to the working directory.
            //Defines.Add(ManagedDebuggingIsEnabled, "IL2CPP_MONO_DEBUGGER_LOGFILE=il2cpp-debugger.log");
            
            Defines.Add(c => ((DotsRuntimeNativeProgramConfiguration)c).CSharpConfig.DotsConfiguration != DotsConfiguration.Release, "IL2CPP_TINY_DEBUG_METADATA");
            CompilerSettings().Add(ManagedDebuggingIsEnabled, c => c.WithExceptions(true));
            CompilerSettings().Add(ManagedDebuggingIsEnabled, c => c.WithRTTI(true));
            IncludeDirectories.Add(ManagedDebuggingIsEnabled, Distribution.Path.Combine("libil2cpp/pch"));
            this.CompilerSettingsForMsvc().Add(c => c.WithWarningPolicies(new [] { new WarningAndPolicy("4102", WarningPolicy.Silent) }));
            CompilerSettings().Add(s => s.WithCppLanguageVersion(CppLanguageVersion.Cpp11));
            this.CompilerSettingsForGcc().Add(s => s.WithWarningPolicies(GetGccLikeWarningPolicies()));
            NativeJobsPrebuiltLibrary.AddToNativeProgram(this); // Only required for managed debugging
        }



        public void SetupConditionalSourcesAndLibrariesForConfig(DotsRuntimeCSharpProgramConfiguration config, DotNetAssembly setupGame)
        {
            NPath[] il2cppGeneratedFiles = SetupInvocation(setupGame, config);
            //todo: stop comparing identifier.
            Sources.Add(npc => ((DotsRuntimeNativeProgramConfiguration)npc).CSharpConfig == config, il2cppGeneratedFiles);
            var staticLibs = setupGame.RecursiveRuntimeDependenciesIncludingSelf.SelectMany(r=>r.Deployables.OfType<StaticLibrary>());
            Libraries.Add(npc => ((DotsRuntimeNativeProgramConfiguration)npc).CSharpConfig == config, staticLibs);

            // force pinvoke internal for static libraries
            staticLibs.ForEach(l => Defines.Add(npc => ((DotsRuntimeNativeProgramConfiguration)npc).CSharpConfig == config, PinvokeInternalDefineFor(l)));

            if (config.EnableManagedDebugging)
                SupportFiles.Add(c => c == config.NativeProgramConfiguration, new DeployableFile(Il2CppTargetDirForAssembly(setupGame).Combine(metadataFilePath), new NPath(metadataFilePath)));
        }

        private string PinvokeInternalDefineFor(IDeployable staticLib)
        {
            var validLib = new NPath(staticLib.ToString()).FileNameWithoutExtension.Replace('-', '_').Replace('.', '_');
            return $"FORCE_PINVOKE_{validLib}_INTERNAL=1";
        }
    }

    public static void AddLibIl2CppAsLibraryFor(NativeProgram program)
    {
        program.Libraries.Add(c => !ManagedDebuggingIsEnabled(c), LibIL2Cpp);
        program.Libraries.Add(ManagedDebuggingIsEnabled, BigLibIL2Cpp);    
    }
    
    public static NPath Il2CppTargetDirForAssembly(DotNetAssembly inputAssembly)
    {
        return inputAssembly.Path.Parent.Combine("il2cpp-src");
    }

    public static NPath[] SetupInvocation(DotNetAssembly inputAssembly, DotsRuntimeCSharpProgramConfiguration config)
    {
        var profile = "unitytiny";
        var il2CppTargetDir = Il2CppTargetDirForAssembly(inputAssembly);
        var enableDevelopmentMode =
            config.EnableUnityCollectionsChecks || config.DotsConfiguration != DotsConfiguration.Release;

        var args = new List<string>()
        {
            "--convert-to-cpp",
            "--disable-cpp-chunks",

            //  "--directory", $"{InputAssembly.Path.Parent}",
            "--generatedcppdir",
            $"{il2CppTargetDir}",

            // Make settings out of these
            $"--dotnetprofile={profile}", // Resolve from DotNetAssembly
            "--libil2cpp-static",
            "--emit-null-checks=0",
            "--enable-array-bounds-check=0",
            "--enable-predictable-output",
            //"--enable-stacktrace=1"
            //"--profiler-report",
            //"--enable-stats",
        };

        if (config.EnableManagedDebugging)
            args.Add("--enable-debugger");

        if (enableDevelopmentMode)
            args.Add("--development-mode");

        var iarrdis = MoveExeToFront(inputAssembly.RecursiveRuntimeDependenciesIncludingSelf);
        args.AddRange(
            iarrdis.SelectMany(a =>
                new[] {"--assembly", a.Path.ToString()}));

        var sharedFileNames = new List<string>
        {
            // static files
            //"Il2CppComCallableWrappers.cpp",
            //"Il2CppProjectedComCallableWrapperMethods.cpp",
            "driver.cpp",
            "GenericMethods.cpp",
            "GenericMethods1.cpp",
            "GenericMethods2.cpp",
            "GenericMethods3.cpp",
            "GenericMethods4.cpp",
            "GenericMethods5.cpp",
            "GenericMethods6.cpp",
            "GenericMethods7.cpp",
            "GenericMethods8.cpp",
            "GenericMethods9.cpp",
            "Generics.cpp",
            "Generics1.cpp",
            "Generics2.cpp",
            "Generics3.cpp",
            "Generics4.cpp",
            "Generics5.cpp",
            "Generics6.cpp",
            "Generics7.cpp",
            "Generics8.cpp",
            "Generics9.cpp",
            "Il2CppGenericComDefinitions.cpp",
        };

        var developmentModeExtraFileNames = new[]
        {
            "TinyMethods.cpp",
        };

        var nonDebuggerExtraFileNames = new[]
        {
            "TinyTypes.cpp",
            "StaticConstructors.cpp",
            "ModuleInitializers.cpp",
            "StringLiterals.cpp",
            "StaticInitialization.cpp",
        };

        var debuggerExtraFileNames = new[]
        {
            "Il2CppGenericClassTable.c",
            "Il2CppGenericInstDefinitions.c",
            "Il2CppGenericMethodDefinitions.c",
            "Il2CppGenericMethodTable.c",
            "Il2CppMetadataRegistration.c",
            "Il2CppMetadataUsage.c",
            "Il2CppTypeDefinitions.c",
            "Il2CppCodeRegistration.cpp",
            "Il2CppCCTypeValuesTable.cpp",
            "Il2CppCCalculateTypeValues.cpp",
            "Il2CppGenericMethodPointerTable.cpp",
            "Il2CppInteropDataTable.cpp",
            "Il2CppInvokerTable.cpp",
            "Il2CppReversePInvokeWrapperTable.cpp",
            "UnresolvedVirtualCallStubs.cpp",
        };

        IEnumerable<string> il2cppOutputFileNames = sharedFileNames;


        if (config.EnableManagedDebugging)
        {
            il2cppOutputFileNames = il2cppOutputFileNames.Concat(debuggerExtraFileNames);
            il2cppOutputFileNames = il2cppOutputFileNames.Concat(iarrdis.Select(asm => asm.Path.FileNameWithoutExtension + "_Debugger.c"));
            il2cppOutputFileNames = il2cppOutputFileNames.Concat(iarrdis.Select(asm => asm.Path.FileNameWithoutExtension + "_Codegen.c"));
            il2cppOutputFileNames = il2cppOutputFileNames.Concat(iarrdis.Select(asm => asm.Path.FileNameWithoutExtension + "_Attr.cpp"));
        }
        else
        {
            il2cppOutputFileNames = il2cppOutputFileNames.Concat(nonDebuggerExtraFileNames);
        }

        if (enableDevelopmentMode && !config.EnableManagedDebugging)
            il2cppOutputFileNames = il2cppOutputFileNames.Concat(developmentModeExtraFileNames);

        var il2cppOutputFiles = il2cppOutputFileNames.Concat(iarrdis.Select(asm => asm.Path.FileNameWithoutExtension + ".cpp"))
            .Select(il2CppTargetDir.Combine).ToArray();

        var il2cppInputs = Distribution.GetFileList("build/deploy/net471")
            .Concat(iarrdis.SelectMany(a => a.Paths))
            .Concat(new[] {Distribution.Path.Combine("libil2cpptiny", "libil2cpptiny.icalls")});

        var finalOutputFiles = il2cppOutputFiles;
        if (config.EnableManagedDebugging)
            finalOutputFiles = finalOutputFiles.Concat(new[] {il2CppTargetDir.Combine(metadataFilePath)}).ToArray();

        Backend.Current.AddAction(
            "Il2Cpp",
            targetFiles:finalOutputFiles,
            inputs: il2cppInputs.ToArray(),
            Il2CppRunnableProgram.InvocationString,
            args.ToArray());

        return il2cppOutputFiles;
    }

    public static NPath CopyIL2CPPMetadataFile(NPath destination, DotNetAssembly inputAssembly)
    {
        var target = destination.Combine(metadataFilePath);
        CopyTool.Instance().Setup(target,
            Il2CppTargetDirForAssembly(inputAssembly).Combine(metadataFilePath));
        return target;
    }

    private static IEnumerable<DotNetAssembly> MoveExeToFront(IEnumerable<DotNetAssembly> assemblies)
    {
        bool foundExe = false;
        var storage = new List<DotNetAssembly>();
        foreach (var a in assemblies)
        {
            if (foundExe)
            {
                yield return a;
                continue;
            }

            if (!a.Path.HasExtension("exe"))
            {
                storage.Add(a);
                continue;
            }
            
            yield return a;
            foreach (var s in storage)
                yield return s;
            foundExe = true;
        }

        if (!foundExe)
            throw new InvalidProgramException("Couldn't find any assembly with .exe suffix in input list; are you missing an entry point?");
    }

    public static NativeProgram LibIL2Cpp => _libil2cpp.Value;
    public static NativeProgram BigLibIL2Cpp => _biglibil2cpp.Value;

    static Lazy<NativeProgram> _libil2cpp = new Lazy<NativeProgram>(()=>CreateLibIl2CppProgram(useExceptions: false));
    static Lazy<NativeProgram> _biglibil2cpp = new Lazy<NativeProgram>(()=>CreateLibIl2CppProgram(useExceptions: true, libil2cppname:"libil2cpp"));
    
    public static NativeProgram BoehmGCProgram => _boehmGCProgram.Value;
    static Lazy<NativeProgram> _boehmGCProgram = new Lazy<NativeProgram>(()=>CreateBoehmGcProgram(Distribution.Path.Combine("external/bdwgc")));


    static NativeProgram CreateLibIl2CppProgram(bool useExceptions, NativeProgram boehmGcProgram = null, string libil2cppname = "libil2cpptiny")
    {
        var fileList = Distribution.GetFileList(libil2cppname).ToArray();

        var nPaths = fileList.Where(f => f.HasExtension("cpp")).ToArray();
        var win32Sources = nPaths.Where(p => p.HasDirectory("Win32")).ToArray();
        var posixSources = nPaths.Where(p => p.HasDirectory("Posix")).ToArray();
        nPaths = nPaths.Except(win32Sources).Except(posixSources).ToArray();

        var program = new NativeProgram(libil2cppname)
        {
            Sources =
            {
                nPaths,
                {c => c.Platform.HasPosix, posixSources},
                {c => c.Platform is WindowsPlatform, win32Sources}
            },
            Exceptions = { useExceptions },
            PublicIncludeDirectories =
                {
                Distribution.Path.Combine(libil2cppname),
                Distribution.Path.Combine("libil2cpp")
                },
            PublicDefines =
            {
                "NET_4_0",
                "GC_NOT_DLL",
                "RUNTIME_IL2CPP",

                "LIBIL2CPP_IS_IN_EXECUTABLE=1",
                {c => c.ToolChain is VisualStudioToolchain, "NOMINMAX", "WIN32_THREADS", "IL2CPP_TARGET_WINDOWS=1"},
                {c => c.CodeGen == CodeGen.Debug, "DEBUG", "IL2CPP_DEBUG"},
                {c => ((DotsRuntimeNativeProgramConfiguration)c).CSharpConfig.DotsConfiguration != DotsConfiguration.Release, "IL2CPP_TINY_DEBUG_METADATA"},
            },
            Libraries =
            {
                {
                    c => c.Platform is WindowsPlatform,
                    new[]
                    {
                        "user32.lib", "advapi32.lib", "ole32.lib", "oleaut32.lib", "Shell32.lib", "Crypt32.lib",
                        "psapi.lib", "version.lib", "MsWSock.lib", "ws2_32.lib", "Iphlpapi.lib", "Dbghelp.lib"
                    }.Select(s => new SystemLibrary(s))
                },
                {c => c.Platform is MacOSXPlatform, new PrecompiledLibrary[] {new SystemFramework("CoreFoundation")}},
                {c => c.Platform is LinuxPlatform, new SystemLibrary("dl")},
                {c => c.Platform is AndroidPlatform, new[] { new SystemLibrary("log")}}
            }
        };

        program.Libraries.Add(BoehmGCProgram);

        program.RTTI.Set(c => useExceptions && c.ToolChain.EnablingExceptionsRequiresRTTI);

        if (libil2cppname == "libil2cpptiny")
        {
            program.Sources.Add(Distribution.GetFileList("libil2cpp/os"));
            program.Sources.Add(Distribution.GetFileList("libil2cpp/gc"));
            program.Sources.Add(Distribution.GetFileList("libil2cpp/utils"));
            program.Sources.Add(Distribution.GetFileList("libil2cpp/vm-utils"));
            program.Sources.Add(Distribution.GetFileList("libil2cpp/codegen"));
            program.PublicIncludeDirectories.Add(Distribution.Path.Combine("libil2cpp"));
            program.PublicIncludeDirectories.Add(Distribution.Path.Combine("libil2cpp", "pch"));
        }
        else
        {
            program.Defines.Add(ManagedDebuggingIsEnabled,
                "IL2CPP_MONO_DEBUGGER=1",
                "PLATFORM_UNITY",
                "UNITY_USE_PLATFORM_STUBS",
                "ENABLE_OVERRIDABLE_ALLOCATORS",
                "IL2CPP_ON_MONO=1",
                "DISABLE_JIT=1",
                "DISABLE_REMOTING=1",
                "HAVE_CONFIG_H",
                "MONO_DLL_EXPORT=1");

            program.Defines.Add(c => c.ToolChain.Platform is WebGLPlatform && ManagedDebuggingIsEnabled(c),
                "HOST_WASM=1");


            program.IncludeDirectories.Add(ManagedDebuggingIsEnabled,
            new[]
            {
                Distribution.Path.Combine("external/mono/mono/eglib"),
                Distribution.Path.Combine("external/mono/mono"),
                Distribution.Path.Combine("external/mono/"),
                Distribution.Path.Combine("external/mono/mono/sgen"),
                Distribution.Path.Combine("external/mono/mono/utils"),
                Distribution.Path.Combine("external/mono/mono/metadata"),
                Distribution.Path.Combine("external/mono/metadata/private"),
                Distribution.Path.Combine("libmono/config"),
                Distribution.Path.Combine("libil2cpp/os/c-api"),
                Distribution.Path.Combine("libil2cpp/pch"),
            });

            var MonoSourceDir = Distribution.Path.Combine("external/mono");
            program.Sources.Add(ManagedDebuggingIsEnabled,
            new []
            {
                "mono/eglib/garray.c",
                "mono/eglib/gbytearray.c",
                "mono/eglib/gdate-unity.c",
                "mono/eglib/gdir-unity.c",
                "mono/eglib/gerror.c",
                "mono/eglib/gfile-unity.c",
                "mono/eglib/gfile.c",
                "mono/eglib/ghashtable.c",
                "mono/eglib/giconv.c",
                "mono/eglib/glist.c",
                "mono/eglib/gmarkup.c",
                "mono/eglib/gmem.c",
                "mono/eglib/gmisc-unity.c",
                "mono/eglib/goutput.c",
                "mono/eglib/gpath.c",
                "mono/eglib/gpattern.c",
                "mono/eglib/gptrarray.c",
                "mono/eglib/gqsort.c",
                "mono/eglib/gqueue.c",
                "mono/eglib/gshell.c",
                "mono/eglib/gslist.c",
                "mono/eglib/gspawn.c",
                "mono/eglib/gstr.c",
                "mono/eglib/gstring.c",
                "mono/eglib/gunicode.c",
                "mono/eglib/gutf8.c",
                "mono/metadata/mono-hash.c",
                "mono/metadata/profiler.c",
                "mono/mini/debugger-agent.c",
                "mono/utils/atomic.c",
                "mono/utils/bsearch.c",
                "mono/utils/dlmalloc.c",
                "mono/utils/hazard-pointer.c",
                "mono/utils/json.c",
                "mono/utils/lock-free-alloc.c",
                "mono/utils/lock-free-array-queue.c",
                "mono/utils/lock-free-queue.c",
                "mono/utils/memfuncs.c",
                "mono/utils/mono-codeman.c",
                "mono/utils/mono-conc-hashtable.c",
                "mono/utils/mono-context.c",
                "mono/utils/mono-counters.c",
                "mono/utils/mono-dl.c",
                "mono/utils/mono-error.c",
                "mono/utils/mono-filemap.c",
                "mono/utils/mono-hwcap.c",
                "mono/utils/mono-internal-hash.c",
                "mono/utils/mono-io-portability.c",
                "mono/utils/mono-linked-list-set.c",
                "mono/utils/mono-log-common.c",
                "mono/utils/mono-logger.c",
                "mono/utils/mono-math.c",
                "mono/utils/mono-md5.c",
                "mono/utils/mono-mmap-windows.c",
                "mono/utils/mono-mmap.c",
                "mono/utils/mono-networkinterfaces.c",
                "mono/utils/mono-os-mutex.c",
                "mono/utils/mono-path.c",
                "mono/utils/mono-poll.c",
                "mono/utils/mono-proclib-windows.c",
                "mono/utils/mono-proclib.c",
                "mono/utils/mono-property-hash.c",
                "mono/utils/mono-publib.c",
                "mono/utils/mono-sha1.c",
                "mono/utils/mono-stdlib.c",
                "mono/utils/mono-threads-coop.c",
                "mono/utils/mono-threads-state-machine.c",
                "mono/utils/mono-threads.c",
                "mono/utils/mono-tls.c",
                "mono/utils/mono-uri.c",
                "mono/utils/mono-value-hash.c",
                "mono/utils/monobitset.c",
                "mono/utils/networking-missing.c",
                "mono/utils/networking.c",
                "mono/utils/parse.c",
                "mono/utils/strenc.c",
                "mono/utils/unity-rand.c",
                "mono/utils/unity-time.c",
                "mono/utils/mono-dl-unity.c",
                "mono/utils/mono-log-unity.c",
                "mono/utils/mono-threads-unity.c",
                "mono/utils/networking-unity.c",
                "mono/utils/os-event-unity.c",
                "mono/metadata/console-unity.c",
                "mono/metadata/file-mmap-unity.c",
                "mono/metadata/w32error-unity.c",
                "mono/metadata/w32event-unity.c",
                "mono/metadata/w32file-unity.c",
                "mono/metadata/w32mutex-unity.c",
                "mono/metadata/w32process-unity.c",
                "mono/metadata/w32semaphore-unity.c",
                "mono/metadata/w32socket-unity.c"
            }.Select(path => MonoSourceDir.Combine(path)));

            program.Sources.Add(c=>c.ToolChain.Platform is WindowsPlatform && ManagedDebuggingIsEnabled(c), MonoSourceDir.Combine("mono/eglib/gunicode-win32.c"));
            program.Sources.Add(c=>c.ToolChain.Platform is WindowsPlatform && ManagedDebuggingIsEnabled(c), MonoSourceDir.Combine("mono/utils/mono-os-wait-win32.c"));

            program.Sources.Add(c=>c.ToolChain.Platform is WebGLPlatform && ManagedDebuggingIsEnabled(c), MonoSourceDir.Combine("mono/utils/mono-hwcap-web.c"));

            program.Sources.Add(c=>c.ToolChain.Architecture is IntelArchitecture && ManagedDebuggingIsEnabled(c), MonoSourceDir.Combine("mono/utils/mono-hwcap-x86.c"));
            program.Sources.Add(c=>c.ToolChain.Architecture is ARMv7Architecture && ManagedDebuggingIsEnabled(c), MonoSourceDir.Combine("mono/utils/mono-hwcap-arm.c"));
            program.Sources.Add(c=>c.ToolChain.Architecture is Arm64Architecture && ManagedDebuggingIsEnabled(c), MonoSourceDir.Combine("mono/utils/mono-hwcap-arm64.c"));

            program.IncludeDirectories.Add(ManagedDebuggingIsEnabled, Distribution.Path.Combine("libil2cpp/debugger"));
        }

        program.PublicDefines.Add("IL2CPP_TINY");
        program.PublicIncludeDirectories.Add(Distribution.Path.Combine("external").Combine("xxHash"));
        program.CompilerSettings().Add(s => s.WithCppLanguageVersion(CppLanguageVersion.Cpp11));
        
        program.CompilerSettingsForGcc().Add(s => s.WithWarningPolicies(GetGccLikeWarningPolicies()));
        
        // Use Baselib headers and library code from the NativeJobs library.
        NativeJobsPrebuiltLibrary.AddToNativeProgram(program);

        //program.CompilerSettingsForMsvc().Add(l => l.WithCompilerRuntimeLibrary(CompilerRuntimeLibrary.None));

        return program;
    }

    public static bool ManagedDebuggingIsEnabled(NativeProgramConfiguration c)
    {
        return ((DotsRuntimeNativeProgramConfiguration)c).CSharpConfig.EnableManagedDebugging;
    }

    public static NativeProgram CreateBoehmGcProgram(NPath boehmGcRoot)
    {
        var program = new NativeProgram("boehm-gc");

        program.Sources.Add($"{boehmGcRoot}/extra/gc.c");
        program.PublicIncludeDirectories.Add($"{boehmGcRoot}/include");
        program.IncludeDirectories.Add($"{boehmGcRoot}/libatomic_ops/src");
        program.Defines.Add(
            "ALL_INTERIOR_POINTERS=1",
            "GC_GCJ_SUPPORT=1",
            "JAVA_FINALIZATION=1",
            "NO_EXECUTE_PERMISSION=1",
            "GC_NO_THREADS_DISCOVERY=1",
            "IGNORE_DYNAMIC_LOADING=1",
            "GC_DONT_REGISTER_MAIN_STATIC_DATA=1",
            "NO_DEBUGGING=1",
            "GC_VERSION_MAJOR=7",
            "GC_VERSION_MINOR=7",
            "GC_VERSION_MICRO=0",
            "HAVE_BDWGC_GC",
            "HAVE_BOEHM_GC",
            "DEFAULT_GC_NAME=\"BDWGC\"",
            "NO_CRT=1",
            "DONT_USE_ATEXIT=1",
            "NO_GETENV=1");

        program.Defines.Add(c => !(c.Platform is WebGLPlatform), "GC_THREADS=1", "USE_MMAP=1", "USE_MUNMAP=1");
        program.Defines.Add(c => c.ToolChain is VisualStudioToolchain, "NOMINMAX", "WIN32_THREADS");
        //program.CompilerSettingsForMsvc().Add(l => l.WithCompilerRuntimeLibrary(CompilerRuntimeLibrary.None));
        return program;
    }



/*

public static BuiltNativeProgram SetupMapFileParser(NPath mapFileParserRoot, CodeGen codegen = CodeGen.Release)
{
    var toolchain = ToolChain.Store.Host();
    var mapFileParserProgram = new NativeProgram("MapFileParser");
    mapFileParserProgram.Sources.Add(mapFileParserRoot.Files("*.cpp", true));
    mapFileParserProgram.Exceptions.Set(true);
    mapFileParserProgram.RTTI.Set(c => c.ToolChain.EnablingExceptionsRequiresRTTI);
    mapFileParserProgram.Libraries.Add(c => c.Platform is WindowsPlatform, new SystemLibrary("Shell32.lib"));
    return mapFileParserProgram.SetupSpecificConfiguration(new NativeProgramConfiguration(codegen, toolchain, false), toolchain.ExecutableFormat);
}

public static BuiltNativeProgram SetupLibIl2CppLackey(NPath libIl2CppLackeyRoot, WindowsToolchain toolchain)
{
    var program = new NativeProgram("libil2cpp-lackey");
    program.Sources.Add($"{libIl2CppLackeyRoot}/DllMain.cpp");
    program.DynamicLinkerSettingsForWindows().Add(l => l.WithEntryPoint("DllMain"));
    return program.SetupSpecificConfiguration(new NativeProgramConfiguration(CodeGen.Release, toolchain, false), toolchain.DynamicLibraryFormat);
}

public static NPath SetupSymbolMap(NPath executableMapFile, NPath mapFileParserExe, ToolChain toolchain)
{
    var mapFileFormat = toolchain.CppCompiler is MsvcCompiler ? "MSVC" :
        toolchain.CppCompiler is ClangCompiler ? "Clang" :
        toolchain.CppCompiler is GccCompiler ? "GCC" : throw new Exception("Unknown map file format");

    var executableSymbolMap = executableMapFile.Parent.Combine("Data/SymbolMap");
    Backend.Current.AddAction(
        "ConvertSymbolMap",
        new[] {executableSymbolMap},
        new[] {mapFileParserExe, executableMapFile},
        mapFileParserExe.InQuotes(),
        new[] {$"-format={mapFileFormat}", executableMapFile.InQuotes(), executableSymbolMap.InQuotes()});
    return executableSymbolMap;
}

public static NativeProgram CreateBoehmGcProgram(NPath boehmGcRoot)
{
    var program = new NativeProgram("boehm-gc");

    program.Sources.Add($"{boehmGcRoot}/extra/gc.c");
    program.PublicIncludeDirectories.Add($"{boehmGcRoot}/include");
    program.IncludeDirectories.Add($"{boehmGcRoot}/libatomic_ops/src");
    program.Defines.Add(
        "ALL_INTERIOR_POINTERS=1",
        "GC_GCJ_SUPPORT=1",
        "JAVA_FINALIZATION=1",
        "NO_EXECUTE_PERMISSION=1",
        "GC_NO_THREADS_DISCOVERY=1",
        "IGNORE_DYNAMIC_LOADING=1",
        "GC_DONT_REGISTER_MAIN_STATIC_DATA=1",
        "NO_DEBUGGING=1",
        "GC_VERSION_MAJOR=7",
        "GC_VERSION_MINOR=7",
        "GC_VERSION_MICRO=0",
        "HAVE_BDWGC_GC",
        "HAVE_BOEHM_GC",
        "DEFAULT_GC_NAME=\"BDWGC\"",
        "NO_CRT=1",
        "DONT_USE_ATEXIT=1",
        "NO_GETENV=1");

    program.Defines.Add(c => !(c.Platform is WebGLPlatform), "GC_THREADS=1", "USE_MMAP=1", "USE_MUNMAP=1");
    program.Defines.Add(c => c.ToolChain is VisualStudioToolchain, "NOMINMAX", "WIN32_THREADS");
    //program.CompilerSettingsForMsvc().Add(l => l.WithCompilerRuntimeLibrary(CompilerRuntimeLibrary.None));
    return program;
}

*/


/*
public static DotNetAssembly SetupLinker(DotNetAssembly inputAssembly, NativeProgramConfiguration nativeProgramConfiguration)
{
    var linkerAssembly = new DotNetAssembly(Distribution.Path.Combine("build/UnityLinker.exe"), Framework.Framework471);
    var linker = new DotNetRunnableProgram(linkerAssembly);

    var outputDir = inputAssembly.Path.Parent.Combine("linkeroutput");

    // combine input files with overrides
    var inputFiles = inputAssembly.RecursiveRuntimeDependenciesIncludingSelf.ToList();
    var nonMainInputs = inputFiles.Exclude(inputAssembly);
    var nonMainOutputs = nonMainInputs.Select(a => Clone(outputDir, a)).ToArray();

    var newDeploy = inputFiles.SelectMany(f => f.Deployables.Where(d=>!(d is DotNetAssembly))).Distinct().ToArray();

    var mainTargetFile = Clone(outputDir, inputAssembly).WithRuntimeDependencies(nonMainOutputs)
        .WithDeployables(newDeploy);

    NPath bclDir = Il2CppDependencies.Path.Combine("MonoBleedingEdge/builds/monodistribution/lib/mono/unityaot");

    var dotNetDeps = new[] {"mscorlib.dll", "System.dll", "System.Configuration.dll", "System.Xml.dll", "System.Core.dll"};
    var isFrameworkNone = inputAssembly.Framework is FrameworkNone;
    var bcl = isFrameworkNone
        ? Array.Empty<DotNetAssembly>()
        : dotNetDeps
        .Select(f => new DotNetAssembly(outputDir.Combine(f), Framework.Framework46)).ToArray();

    var inputPaths = Unity.BuildTools.EnumerableExtensions.Append(inputFiles, linkerAssembly).SelectMany(a => a.Paths);
    inputPaths = Unity.BuildTools.EnumerableExtensions.Append(inputPaths, bcl.Select(d => d.Path).ToArray());

    var linkerArguments = new List<string>
    {
        $"--include-public-assembly={inputAssembly.Path.InQuotes()}",
        $"--out={outputDir.InQuotes()}",
        "--use-dots-options",
        "--dotnetprofile=" + (isFrameworkNone ? "unitydots" : "unityaot"),
        "--rule-set=experimental" // This will enable modification of method bodies to further reduce size.
    };

    foreach (var inputDirectory in inputFiles.Select(f => f.Path.Parent).Distinct())
        linkerArguments.Add($"--include-directory={inputDirectory.InQuotes()}");

    if (!isFrameworkNone)
        linkerArguments.Add($"--search-directory={bclDir.InQuotes()}");

    var targetPlatform = GetTargetPlatformForLinker(nativeProgramConfiguration.Platform);
    if (!string.IsNullOrEmpty(targetPlatform))
        linkerArguments.Add($"--platform={targetPlatform}");

    var targetArchitecture = GetTargetArchitectureForLinker(nativeProgramConfiguration.ToolChain.Architecture);
    if (!string.IsNullOrEmpty(targetPlatform))
        linkerArguments.Add($"--architecture={targetArchitecture}");

    var targetFiles = Unity.BuildTools.EnumerableExtensions.Prepend(nonMainOutputs, mainTargetFile);
    targetFiles = targetFiles.Append(bcl);
    Backend.Current.AddAction(
        "UnityLinker",
        targetFiles: targetFiles.SelectMany(a=>a.Paths).ToArray(),
        inputs: inputPaths.ToArray(),
        executableStringFor: linker.InvocationString,
        commandLineArguments: linkerArguments.ToArray(),
        allowUnwrittenOutputFiles: false,
        allowUnexpectedOutput: false,
        allowedOutputSubstrings: new[] {"Output action"});


    return mainTargetFile.WithRuntimeDependencies(bcl).DeployTo(inputAssembly.Path.Parent.Combine("finaloutput"));
}
*/

    public static class UnityLinker
    {
        public static DotNetAssembly SetupInvocation(DotNetAssembly inputGame, NPath outputPath, NativeProgramConfiguration config)
        {
            return inputGame.ApplyDotNetAssembliesPostProcessor(outputPath,(inputAssemblies, targetDir) => AddActions(inputAssemblies, targetDir, config)
            );
        }

        static void AddActions(DotNetAssembly[] inputAssemblies, NPath targetDirectory, NativeProgramConfiguration nativeProgramConfiguration)
        {
            var linkerAssembly = new DotNetAssembly(Distribution.Path.Combine("build/deploy/net471/UnityLinker.exe"), Framework.Framework471);
            var linker = new DotNetRunnableProgram(linkerAssembly);

            
            var outputDir = targetDirectory;
            var isFrameworkNone = inputAssemblies.First().Framework == Framework.FrameworkNone;

            var rootAssemblies = inputAssemblies.Where(a => a.Path.HasExtension("exe")).Concat(new[]{inputAssemblies.First()}).Distinct();
            
            var linkerArguments = new List<string>
            {
                $"--out={outputDir.InQuotes()}",
                "--use-dots-options",
                "--dotnetprofile=" + (isFrameworkNone ? "unitytiny" : "unityaot"),
            };

            linkerArguments.AddRange(rootAssemblies.Select(rootAssembly => $"--include-public-assembly={rootAssembly.Path.InQuotes()}"));
            
            foreach (var inputDirectory in inputAssemblies.Select(f => f.Path.Parent).Distinct())
                linkerArguments.Add($"--include-directory={inputDirectory.InQuotes()}");

            NPath bclDir = Il2CppDependencies.Path.Combine("MonoBleedingEdge/builds/monodistribution/lib/mono/unityaot");

            if (!isFrameworkNone)
                linkerArguments.Add($"--search-directory={bclDir.InQuotes()}");

            var targetPlatform = GetTargetPlatformForLinker(nativeProgramConfiguration.Platform);
            if (!string.IsNullOrEmpty(targetPlatform))
                linkerArguments.Add($"--platform={targetPlatform}");

            var targetArchitecture = GetTargetArchitectureForLinker(nativeProgramConfiguration.ToolChain.Architecture);
            if (!string.IsNullOrEmpty(targetPlatform))
                linkerArguments.Add($"--architecture={targetArchitecture}");

            if (ManagedDebuggingIsEnabled(nativeProgramConfiguration))
            {
                linkerArguments.Add("--rule-set=aggressive"); // Body modification causes debug symbols to be out of sync
                linkerArguments.Add("--enable-debugger");
            }
            else
            {
                linkerArguments.Add("--rule-set=experimental"); // This will enable modification of method bodies to further reduce size.
            }

  //          var targetFiles = Unity.BuildTools.EnumerableExtensions.Prepend(nonMainOutputs, mainTargetFile);
  //          targetFiles = targetFiles.Append(bcl);
              var targetFiles = inputAssemblies.SelectMany(a=>a.Paths).Select(i => targetDirectory.Combine(i.FileName)).ToArray();

              Backend.Current.AddAction(
                "UnityLinker",
                targetFiles: targetFiles,
                inputs: inputAssemblies.SelectMany(a=>a.Paths).Concat(linkerAssembly.Paths).ToArray(),
                executableStringFor: linker.InvocationString,
                commandLineArguments: linkerArguments.ToArray(),
                allowUnwrittenOutputFiles: false,
                allowUnexpectedOutput: false,
                allowedOutputSubstrings: new[] {"Output action"});
        }
    }

    static string GetTargetPlatformForLinker(Platform platform)
    {
    // Desktop platforms
    if (platform is WindowsPlatform)
        return "WindowsDesktop";
    if (platform is MacOSXPlatform)
        return "MacOSX";
    if (platform is LinuxPlatform)
        return "Linux";
    if (platform is UniversalWindowsPlatform)
        return "WinRT";

    // mobile
    if (platform is AndroidPlatform)
        return "Android";
    if (platform is IosPlatform)
        return "iOS";

    // consoles
    if (platform is XboxOnePlatform)
        return "XboxOne";
    if (platform is PS4Platform)
        return "PS4";
    if (platform is SwitchPlatform)
        return "Switch";

    // other
    if (platform is WebGLPlatform)
        return "WebGL";
    if (platform is LuminPlatform)
        return "Lumin";

    return null;
}

static string GetTargetArchitectureForLinker(Architecture arch)
{
    if (arch is x64Architecture)
        return "x64";
    if (arch is x86Architecture)
        return "x86";
    if (arch is ARMv7Architecture)
        return "ARMv7";
    if (arch is Arm64Architecture)
        return "ARM64";
    if (arch is EmscriptenArchitecture)
        return "EmscriptenJavaScript";

    return null;
}

private static DotNetAssembly Clone(NPath outputDir, DotNetAssembly a)
{
    var debugSymbolPath = a.DebugSymbolPath == null ? null : outputDir.Combine(a.DebugSymbolPath.FileName);
    return new DotNetAssembly(outputDir.Combine(a.Path.FileName), a.Framework,a.DebugFormat, debugSymbolPath);
}
}
