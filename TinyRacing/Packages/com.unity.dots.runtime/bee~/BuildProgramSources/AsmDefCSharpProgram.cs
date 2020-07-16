using System;
using System.Linq;
using Bee.DotNet;
using NiceIO;
using Unity.BuildSystem.CSharpSupport;

public class AsmDefCSharpProgram : DotsRuntimeCSharpProgram
{
    public DotsRuntimeCSharpProgram[] ReferencedPrograms { get; }
    public AsmDefDescription AsmDefDescription { get; }

    // We don't have the ability to have asmdef references which are required by Hybrid but are incompatible
    // with DOTS Runtime. So we manually remove them here for now :(
    string[] IncompatibleDotRuntimeAsmDefs =
    {
        "Unity.Properties",
        "Unity.Properties.Reflection",
        "Unity.PerformanceTesting"
    };

    public AsmDefCSharpProgram(AsmDefDescription asmDefDescription)
        : base(asmDefDescription.Directory,
            asmDefDescription.IncludedAsmRefs.Select(asmref => asmref.Path.Parent),
            deferConstruction: true)
    {
        AsmDefDescription = asmDefDescription;

        var asmDefReferences = AsmDefDescription.References.Select(asmDefDescription1 => BuildProgram.GetOrMakeDotsRuntimeCSharpProgramFor(asmDefDescription1)).ToList();

        ReferencedPrograms = asmDefReferences.Where(r => !IncompatibleDotRuntimeAsmDefs.Contains(r.AsmDefDescription.Name)).ToArray();

        var isExe = asmDefDescription.DefineConstraints.Contains("UNITY_DOTS_ENTRYPOINT") ||
                    asmDefDescription.Name.EndsWith(".Tests");

        Construct(asmDefDescription.Name, isExe);

        ProjectFile.AdditionalFiles.Add(asmDefDescription.Path);

        IncludePlatforms = AsmDefDescription.IncludePlatforms;
        ExcludePlatforms = AsmDefDescription.ExcludePlatforms;
        Unsafe = AsmDefDescription.AllowUnsafeCode;
        References.Add(config =>
        {
            if (config is DotsRuntimeCSharpProgramConfiguration dotsConfig)
                return ReferencedPrograms.Where(rp => rp.IsSupportedFor(dotsConfig));

            //this codepath will be hit for the bindgem invocation
            return ReferencedPrograms;
        });

        if (AsmDefDescription.IsTinyRoot || isExe)
        {
            AsmDefCSharpProgramCustomizer.RunAllAddPlatformImplementationReferences(this);
        }

        if (BuildProgram.UnityTinyBurst != null)
            References.Add(BuildProgram.UnityTinyBurst);
        if (BuildProgram.ZeroJobs != null)
            References.Add(BuildProgram.ZeroJobs);
        if (BuildProgram.UnityLowLevel != null)
            References.Add(BuildProgram.UnityLowLevel);

        if (IsTestAssembly)
        {
            // Set true to build the Portable runner on dotnet (instead of the NUnit runner).
            // Normally the portable runner is only used for IL2CPP, but debugging the tests
            // and runner is easier on dotnet.
            bool usePortableRunnerOnDotNet = false;

            var nunitLiteMain = BuildProgram.BeeRoot.Combine("CSharpSupport/NUnitLiteMain.cs");
            Sources.Add(nunitLiteMain);

            // Setup for IL2CPP
            var tinyTestFramework = BuildProgram.BeeRoot.Parent.Combine("TinyTestFramework");
            Sources.Add(c => ((DotsRuntimeCSharpProgramConfiguration)c).ScriptingBackend == ScriptingBackend.TinyIl2cpp || usePortableRunnerOnDotNet , tinyTestFramework);
            Defines.Add(c => ((DotsRuntimeCSharpProgramConfiguration)c).ScriptingBackend == ScriptingBackend.TinyIl2cpp || usePortableRunnerOnDotNet , "UNITY_PORTABLE_TEST_RUNNER");

            // Setup for dotnet
            References.Add(c => ((DotsRuntimeCSharpProgramConfiguration)c).ScriptingBackend == ScriptingBackend.Dotnet && !usePortableRunnerOnDotNet , BuildProgram.NUnitFramework);
            ProjectFile.AddCustomLinkRoot(nunitLiteMain.Parent, "TestRunner");
            References.Add(c => ((DotsRuntimeCSharpProgramConfiguration)c).ScriptingBackend == ScriptingBackend.Dotnet && !usePortableRunnerOnDotNet , BuildProgram.NUnitLite);

            // General setup
            References.Add(BuildProgram.GetOrMakeDotsRuntimeCSharpProgramFor(AsmDefConfigFile.AsmDefDescriptionFor("Unity.Entities")));
            References.Add(BuildProgram.GetOrMakeDotsRuntimeCSharpProgramFor(AsmDefConfigFile.AsmDefDescriptionFor("Unity.Tiny.Core")));
            References.Add(BuildProgram.GetOrMakeDotsRuntimeCSharpProgramFor(AsmDefConfigFile.AsmDefDescriptionFor("Unity.Tiny.UnityInstance")));
        }
        else if(IsILPostProcessorAssembly)
        {
            References.Add(BuildProgram.UnityCompilationPipeline);
            References.Add(MonoCecil.Paths);
            References.Add(Il2Cpp.Distribution.Path.Combine("build/deploy/net471/Unity.Cecil.Awesome.dll"));
        }
    }

    public override bool IsSupportedFor(CSharpProgramConfiguration config)
    {
        //UNITY_DOTS_ENTRYPOINT is actually a fake define constraint we use to signal the buildsystem,
        //so don't impose it as a constraint
        return base.IsSupportedFor(config) &&
               AsmDefDescription.DefineConstraints.All(dc =>
                   dc == "UNITY_DOTS_ENTRYPOINT" || Defines.For(config).Contains(dc));
    }

    protected override TargetFramework GetTargetFramework(CSharpProgramConfiguration config, DotsRuntimeCSharpProgram program)
    {
        if (IsILPostProcessorAssembly || (IsTestAssembly && ((DotsRuntimeCSharpProgramConfiguration)config).ScriptingBackend == ScriptingBackend.Dotnet))
        {
            return TargetFramework.NetStandard20;
        }

        return base.GetTargetFramework(config, program);
    }

    public void AddPlatformImplementationFor(string baseFeatureAsmDefName, string platformImplAsmDefName)
    {
        if (AsmDefDescription.Name == platformImplAsmDefName)
            return;

        if (AsmDefDescription.References.Any(r => r.Name == baseFeatureAsmDefName))
        {
            var impl = AsmDefConfigFile.CSharpProgramFor(platformImplAsmDefName);
            if (impl == null)
            {
                Console.WriteLine($"Missing assembly for {platformImplAsmDefName}, named in a customizer for {baseFeatureAsmDefName}.  Are you missing a package, or is the customizer in the wrong place?");
                return;
            }
            References.Add(c => impl.IsSupportedFor(c), impl);
        }
    }

    protected override NPath DeterminePathForProjectFile() =>
        DoesPackageSourceIndicateUserHasControlOverSource(AsmDefDescription.PackageSource)
            ? AsmDefDescription.Path.Parent.Combine(AsmDefDescription.Name + ".gen.csproj")
            : base.DeterminePathForProjectFile();

    public bool IsTestAssembly => AsmDefDescription.OptionalUnityReferences.Contains("TestAssemblies");
    public bool IsILPostProcessorAssembly => AsmDefDescription.Name.EndsWith(".CodeGen");
}
