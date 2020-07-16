using System;
using System.Collections.Generic;
using System.Linq;
using Bee.Core;
using Bee.Stevedore;
using Bee.Toolchain.VisualStudio;
using JetBrains.Annotations;
using NiceIO;
using Unity.BuildSystem.NativeProgramSupport;

public static class NativeJobsPrebuiltLibrary
{
    private static Dictionary<String, NPath> ArtifactPaths = new Dictionary<String, NPath>();
    private static bool UseLocalDev => Environment.GetEnvironmentVariable("NATIVEJOBS_FROM_LOCAL", EnvironmentVariableTarget.User) != null;
    private static NPath LocalDevRoot = BuildProgram.BeeRoot.Parent.Parent.Parent.Parent.Parent.Combine("nativejobs");

    private static NPath GetOrCreateSteveArtifactPath(String name)
    {
        if (!ArtifactPaths.ContainsKey(name))
        {
            var artifact = new StevedoreArtifact(name);
            Backend.Current.Register(artifact);
            ArtifactPaths[name] = artifact.Path;
        }

        return ArtifactPaths[name];
    }

    private static string GetNativeJobsReleaseArchName(NativeProgramConfiguration npc)
    {
        // We need to detect which emscripten backend to use on web builds, but it is only available if the 
        // com.unity.platforms.web package is in the project manifest. Otherwise, we just default to false
        // as it is irrelevant.
        var tinyEmType = Type.GetType("TinyEmscripten");
        bool useWasmBackend = (bool)(tinyEmType?.GetProperty("UseWasmBackend")?.GetValue(tinyEmType) ?? false);
        switch (npc.Platform)
        {
            case MacOSXPlatform _:
                if (npc.ToolChain.Architecture.IsX64) return "mac64";
                break;
            case WindowsPlatform _:
                if (npc.ToolChain.Architecture.IsX86) return "win32";
                if (npc.ToolChain.Architecture.IsX64) return "win64";
                break;
            default:
                if (npc.ToolChain.Architecture.IsX86) return "x86";
                if (npc.ToolChain.Architecture.IsX64) return "x64";
                if (npc.ToolChain.Architecture.IsArmv7) return "arm32";
                if (npc.ToolChain.Architecture.IsArm64) return "arm64";
                if (npc.ToolChain.Architecture is WasmArchitecture && useWasmBackend) return "wasm";
                if (npc.ToolChain.Architecture is WasmArchitecture && !useWasmBackend) return "wasm_fc";
                if (npc.ToolChain.Architecture is AsmJsArchitecture && useWasmBackend) return "asmjs";
                if (npc.ToolChain.Architecture is AsmJsArchitecture && !useWasmBackend) return "asmjs_fc";
                //if (npc.ToolChain.Architecture is WasmArchitecture && HAS_THREADING) return "wasm_withthreads";
                break;
        }

        throw new InvalidProgramException($"Unknown toolchain and architecture for baselib: {npc.ToolChain.LegacyPlatformIdentifier} {npc.ToolChain.Architecture.Name}");
    }

    private static string GetNativeJobsConfigName(NativeProgramConfiguration npc)
    {
        var dotsrtCSharpConfig = ((DotsRuntimeNativeProgramConfiguration)npc).CSharpConfig;

        // If collection checks have been forced on in a release build, swap in the develop version of the native jobs prebuilt lib
        // as the release configuration will not contain the collection checks code paths.
        if (dotsrtCSharpConfig.EnableUnityCollectionsChecks && dotsrtCSharpConfig.DotsConfiguration == DotsConfiguration.Release)
            return DotsConfiguration.Develop.ToString().ToLower();

        return dotsrtCSharpConfig.DotsConfiguration.ToString().ToLower();
    }

    private static NPath GetLibPath(NativeProgramConfiguration c)
    {
        var tinyEmType = Type.GetType("TinyEmscripten");
        bool useWasmBackend = (bool)(tinyEmType?.GetProperty("UseWasmBackend")?.GetValue(tinyEmType) ?? false);
        bool useWebGlThreading = false;

        var staticPlatforms = new[]
        {
            "IOS",
            "WebGL",
        };

        if (UseLocalDev)
        {
            return LocalDevRoot.Combine("artifacts", "nativejobs", GetNativeJobsConfigName(c) + "_" + c.ToolChain.ActionName.ToLower() +
                (c.Platform.Name == "WebGL" && !useWasmBackend ? "_fc" : "") + (useWebGlThreading ? "_withthreads" : "") + "_nonlump" +
                (staticPlatforms.Contains(c.Platform.Name) ? "" : "_dll"));
        }

        var prebuiltLibPath = GetOrCreateSteveArtifactPath($"nativejobs-{c.Platform.Name}" + (staticPlatforms.Contains(c.Platform.Name) ? "-s" : "-d"));
        return prebuiltLibPath.Combine("lib", c.Platform.Name.ToLower(), GetNativeJobsReleaseArchName(c), GetNativeJobsConfigName(c));
    }

    public static void AddToNativeProgram(NativeProgram np)
    {
        np.PublicDefines.Add("BASELIB_USE_DYNAMICLIBRARY=1");
        np.PublicDefines.Add(c => c.Platform is IosPlatform, "FORCE_PINVOKE_nativejobs_INTERNAL=1");

        if (UseLocalDev)
        {
            np.IncludeDirectories.Add(LocalDevRoot.Combine("External", "baselib", "Include"));
            np.IncludeDirectories.Add(c => LocalDevRoot.Combine("External", "baselib", "Platforms", c.Platform.Name, "Include"));
        }
        else
        {
            np.IncludeDirectories.Add(GetOrCreateSteveArtifactPath("nativejobs-all-public").Combine("Include"));
            np.IncludeDirectories.Add(c => GetOrCreateSteveArtifactPath($"nativejobs-{c.Platform.Name}-public").Combine("Platforms", c.Platform.Name, "Include"));
        }

        np.Libraries.Add(c => c.Platform.Name == "Windows", c => new PrecompiledLibrary[] {
                new MsvcDynamicLibrary(GetLibPath(c).Combine("nativejobs.dll")),
                new StaticLibrary(GetLibPath(c).Combine("nativejobs.dll.lib")) });
        np.Libraries.Add(c => c.Platform.Name == "Linux" || c.Platform.Name == "Android", c => new[] {
                new DynamicLibrary(GetLibPath(c).Combine("libnativejobs.so")) });
        np.Libraries.Add(c => c.Platform.Name == "OSX", c => new[] {
                new DynamicLibrary(GetLibPath(c).Combine("libnativejobs.dylib")) });
        np.Libraries.Add(c => c.Platform.Name == "IOS", c => new[] {
                new StaticLibrary(GetLibPath(c).Combine("libnativejobs.a")) });
        np.Libraries.Add(c => c.Platform.Name == "WebGL", c => new[] {
                new StaticLibrary(GetLibPath(c).Combine("libnativejobs.bc")) });
    }

    public static void AddBindings(DotsRuntimeCSharpProgram csp, NPath defaultPath)
    {
        if (UseLocalDev)
        {
            csp.Sources.Add(LocalDevRoot.Combine("External", "baselib", "CSharp", "BindingsPinvoke"));
            csp.Sources.Add(LocalDevRoot.Combine("External", "baselib", "CSharp", "Error.cs"));
            csp.Sources.Add(LocalDevRoot.Combine("External", "baselib", "CSharp", "ManualBindings.cs"));
        }
        else
        {
            csp.Sources.Add(defaultPath);
        }
    }
}
