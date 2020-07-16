using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NiceIO;

static class AsmDefConfigFile
{
    private static JObject Json { get; }
    
    static readonly Dictionary<string,AsmDefDescription> _namesToAsmDefDescription = new Dictionary<string, AsmDefDescription>();
    static readonly Dictionary<NPath,AsmRefDescription> _pathsToAsmRefDescription = new Dictionary<NPath, AsmRefDescription>();
    public static NPath UnityProjectPath { get; }
    public static NPath UnityCompilationPipelineAssemblyPath { get; }
    public static Dictionary<string, string> GuidsToAsmDefNames { get; } = new Dictionary<string, string>();
    public static readonly int BuildSettingsFileVersion;

    static AsmDefConfigFile()
    {
        Json = JObject.Parse(new NPath("asmdefs.json").MakeAbsolute().ReadAllText());
        UnityProjectPath = Json["UnityProjectPath"].Value<string>();
        ProjectName = Json["ProjectName"].Value<string>();
        UnityCompilationPipelineAssemblyPath = Json["CompilationPipelineAssemblyPath"].Value<string>();
        BuildSettingsFileVersion = Json["BuildSettingsFileVersion"].Value<int>();

        // asmrefs have to be created first, because they're used during construction of the asmdef description
        foreach (var asmref in Json["asmrefs"].Values<JObject>())
        {
            var path = asmref["FullPath"].Value<string>().ToNPath();
            var desc = new AsmRefDescription(path, asmref["PackageSource"].Value<string>());
            _pathsToAsmRefDescription[path] = desc;
        }

        // then the Guid mapping has to be set up
        foreach (var asmdef in Json["asmdefs"].Values<JObject>())
        {
            var name = asmdef["AsmdefName"].Value<string>();
            GuidsToAsmDefNames[asmdef["Guid"].Value<string>()] = name;
        }

        // finally we can create the AsmDefDescriptions
        foreach (var asmdef in Json["asmdefs"].Values<JObject>())
        {
            var name = asmdef["AsmdefName"].Value<string>();
            var desc = new AsmDefDescription(asmdef["FullPath"].Value<string>(), asmdef["PackageSource"].Value<string>());
            _namesToAsmDefDescription[name] = desc;
        }
    }

    public static string GetRealAsmDefName(string nameOrGuid)
    {
        if (nameOrGuid.StartsWith("GUID:"))
        {
            if (GuidsToAsmDefNames.TryGetValue(nameOrGuid.Substring(5), out var name))
                return name;
            //Console.WriteLine($"No asmdef found for {nameOrGuid}");
            return null;
        }

        return nameOrGuid;
    }
    
    public static string ProjectName { get; }

    internal static HashSet<string> NotFoundNames = new HashSet<string>();
    public static AsmDefDescription AsmDefDescriptionFor(string asmdefname)
    {
        if (asmdefname == null)
            return null;
        if (_namesToAsmDefDescription.TryGetValue(asmdefname, out var result))
            return result;
        if (!NotFoundNames.Contains(asmdefname))
        {
            //Console.WriteLine($"No asmdef found for {asmdefname}");
            NotFoundNames.Add(asmdefname);
        }

        return null;
    }

    public static DotsRuntimeCSharpProgram CSharpProgramFor(string asmdefname)
    {
        var desc = AsmDefDescriptionFor(asmdefname);
        if (desc == null)
            return null;
        return BuildProgram.GetOrMakeDotsRuntimeCSharpProgramFor(desc);
    }

    public static IEnumerable<AsmDefDescription> AssemblyDefinitions => _namesToAsmDefDescription.Values;

    public static IEnumerable<AsmRefDescription> AsmRefs => _pathsToAsmRefDescription.Values;

    public static IEnumerable<AsmDefDescription> AutoReferencedAssemblyDefinitions => AssemblyDefinitions.Where(desc => desc.AutoReferenced);

    public static IEnumerable<AsmDefDescription> TestableAssemblyDefinitions
    {
        get
        {
            foreach (var asmdefName in Json["Testables"].Values<string>())
                yield return AsmDefDescriptionFor(asmdefName);
        }
    }
}
