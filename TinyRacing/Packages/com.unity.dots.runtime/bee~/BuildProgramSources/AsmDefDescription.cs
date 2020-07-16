using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NiceIO;
using Unity.BuildSystem.NativeProgramSupport;

public class AsmRefDescription
{
    public NPath Path { get; }
    public string PackageSource { get; }
    private JObject Json;
    
    public AsmRefDescription(NPath path, string packageSource)
    {
        Path = path;
        PackageSource = packageSource;
        Json = JObject.Parse(path.ReadAllText());
    }

    public string Reference => AsmDefConfigFile.GetRealAsmDefName(Json["reference"].Value<string>());
}


public class AsmDefDescription
{
    public NPath Path { get; }
    public string PackageSource { get; }
    internal JObject Json;

    public AsmDefDescription(NPath path, string packageSource)
    {
        Path = path;
        PackageSource = packageSource;
        Json = JObject.Parse(path.ReadAllText());
        IncludedAsmRefs = AsmDefConfigFile.AsmRefs.Where(desc => desc.Reference == Name).ToList();
    }

    public string Name => Json["name"].Value<string>();
    public List<AsmRefDescription> IncludedAsmRefs { get; }

    private string[] FixedNamedReferences;
    public string[] NamedReferences
    {
        get
        {
            if (FixedNamedReferences == null)
            {
                FixedNamedReferences = Json["references"]?.Values<string>().Select(AsmDefConfigFile.GetRealAsmDefName).ToArray() ?? Array.Empty<string>();
            }

            return FixedNamedReferences;
        }
    }

    public bool NeedsEntryPointAdded()
    {
        return !DefineConstraints.Contains("UNITY_DOTS_ENTRYPOINT") && References.All(r => r.NeedsEntryPointAdded());
    }

    public AsmDefDescription[] References =>
        NamedReferences.Select(AsmDefConfigFile.AsmDefDescriptionFor)
            .Where(d => d != null && IsSupported(d.Name))
            .ToArray();

    public NPath Directory => Path.Parent;
    public bool IsTinyRoot { get; set; }
    
    public Platform[] IncludePlatforms => ReadPlatformList(Json["includePlatforms"]);
    public Platform[] ExcludePlatforms => ReadPlatformList(Json["excludePlatforms"]);
    public bool AllowUnsafeCode => Json["allowUnsafeCode"]?.Value<bool>() == true;

    public string[] DefineConstraints => Json["defineConstraints"]?.Values<string>().ToArray() ?? Array.Empty<string>();
    public string[] PositiveDefineConstraints => DefineConstraints.Where(s => !s.StartsWith("!")).ToArray();
    public string[] NegativeDefineConstraints => DefineConstraints.Where(s => s.StartsWith("!")).Select(s => s.Substring(1)).ToArray();

    public string[] OptionalUnityReferences => Json["optionalUnityReferences"]?.Values<string>()?.ToArray() ?? Array.Empty<string>();

    public bool IncludeTestAssemblies => OptionalUnityReferences.Contains("TestAssemblies");
    
    public bool AutoReferenced => Json["autoReferenced"]?.Value<bool>() == true;
    public bool NoEngineReferences => Json["noEngineReferences"]?.Value<bool>() == true;
    public bool OverrideReferences => Json["overrideReferences"]?.Value<bool>() == true;
    public string[] PrecompiledReferences => Json["precompiledReferences"]?.Values<string>()?.ToArray() ?? Array.Empty<string>();

    private static Platform[] ReadPlatformList(JToken platformList)
    {
        if (platformList == null)
            return Array.Empty<Platform>();

        return platformList.Select(token => PlatformFromAsmDefPlatformName(token.ToString())).Where(p => p != null).ToArray();
    }

    private static Platform PlatformFromAsmDefPlatformName(string name)
    {
        switch(name)
        {
            case "macOSStandalone":
                return new MacOSXPlatform();
            case "WindowsStandalone32":
            case "WindowsStandalone64":
                return new WindowsPlatform();
            case "Editor":
                return null;
            default:
            {
                var typeName = $"{name}Platform";
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .FirstOrDefault(t => t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));
                if (type == null)
                {
                    Console.WriteLine($"Couldn't find Platform for {name} (tried {name}Platform), ignoring it.");
                    return null;
                }
                return (Platform)Activator.CreateInstance(type);
            }
        }
    }
    private bool IsSupported(string referenceName)
    {
        if (referenceName.Contains("Unity.Collections.Tests"))
            return false;

        return true;
    }
}
