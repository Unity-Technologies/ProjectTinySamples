using System;
using System.Collections.Generic;
using System.Linq;
using NiceIO;
using Unity.BuildSystem.CSharpSupport;
using Bee.Core;
using Bee.CSharpSupport;
using Bee.VisualStudioSolution;
using Bee.DotNet;
using Bee.Stevedore;
using Bee.Tools;
using Bee.TundraBackend;
using Newtonsoft.Json.Linq;
using Unity.BuildTools;
using Unity.BuildSystem.NativeProgramSupport;

class BuildProgramBuildProgram
{
    static CSharpProgramConfiguration CSharpConfig { get; set; }
    
    static void Main(string[] path)
    {
        var bee = new NPath(typeof(CSharpProgram).Assembly.Location);

        StevedoreGlobalSettings.Instance = new StevedoreGlobalSettings
        {
            // Manifest entries always override artifact IDs hard-coded in Bee
            // Setting EnforceManifest to true will also ensure no artifacts
            // are used without being listed in a manifest.
            EnforceManifest = true,
            Manifest =
            {
                bee.Parent.Combine("manifest.stevedore").ToString(),
            },
        };
        //The stevedore global manifest will override DownloadableCsc.Csc72 artifacts and use Csc73
        CSharpConfig = new CSharpProgramConfiguration(CSharpCodeGen.Release, DownloadableCsc.Csc72, DebugFormat.PortablePdb);

        var asmdefsFile = new NPath("asmdefs.json").MakeAbsolute();

        var asmDefInfo = JObject.Parse(asmdefsFile.ReadAllText());
        var asmDefs = asmDefInfo["asmdefs"].Value<JArray>();
        var asmRefs = asmDefInfo["asmrefs"].Value<JArray>();
        var asmDefToAsmRefs = new Dictionary<string, List<string>>();
        foreach (var asmref in asmRefs)
        {
            var target = asmref["AsmRefTarget"].Value<string>();
            if (target.StartsWith("GUID:"))
            {
                target = asmDefs.First(ad => ad["Guid"].Value<string>() == target.Substring(5))["AsmdefName"]
                    .Value<string>();
            }
            List<string> toaddto;
            if (asmDefToAsmRefs.TryGetValue(target, out var list))
            {
                toaddto = list;
            }
            else
            {
                toaddto = new List<string>(); 
                asmDefToAsmRefs[target] = toaddto;
            }
            toaddto.Add(asmref["FullPath"].Value<string>());
        }
          
        var buildProgram = new CSharpProgram()
        {
            Path = path[0],
            Sources = {
                bee.Parent.Combine("BuildProgramSources"),
                asmDefs.SelectMany(asmdef=>
                {
                    var apath = new NPath(asmdef["FullPath"].Value<string>());
                    var beeFiles = HarvestBeeFilesFrom(apath.Parent);
                    var asmdefName = asmdef["AsmdefName"].Value<string>();

                    if (asmDefToAsmRefs.TryGetValue(asmdefName, out var asmrefs))
                    {
                        foreach (var asmref in asmrefs)
                        {
                            beeFiles = beeFiles.Concat(HarvestBeeFilesFrom(asmref.ToNPath().Parent));
                        }
                    }

                    return beeFiles;
                }),
                HarvestBeeFilesFrom(bee.Parent.Parent.Combine("LowLevelSupport~","Unity.ZeroJobs"))
            },
            Framework = {Framework.Framework471},
            LanguageVersion = "7.2",
            References = { bee, new SystemReference("System.Xml.Linq"), new SystemReference("System.Xml") },
            ProjectFile = { Path = NPath.CurrentDirectory.Combine("buildprogram.gen.csproj")}
        };
        
        ((TundraBackend)Backend.Current).AddExtensionToBeScannedByHashInsteadOfTimeStamp("json", "config");
        
        buildProgram.ProjectFile.AddCustomLinkRoot(bee.Parent.Combine("BuildProgramSources"), ".");
        buildProgram.SetupSpecificConfiguration(CSharpConfig);
        
        
        var buildProgrambuildProgram = new CSharpProgram()
        {
            FileName = "buildprogrambuildprogram.exe",
            Sources = {
                bee.Parent.Combine("BuildProgramBuildProgramSources")
            },
            LanguageVersion = "7.2",
            Framework = { Framework.Framework471},
            References = { bee }
        };
        buildProgrambuildProgram.SetupSpecificConfiguration(CSharpConfig);
        
        new VisualStudioSolution()
        {
            Path = "build.gen.sln",
            Projects = { buildProgram, buildProgrambuildProgram }
        }.Setup();
    }

    private static IEnumerable<NPath> HarvestBeeFilesFrom(NPath asmdefDirectory)
    {
        var beeDir = asmdefDirectory.Combine("bee~");
        return !beeDir.DirectoryExists() ? Enumerable.Empty<NPath>() : beeDir.Files("*.cs", true);
    }
}
