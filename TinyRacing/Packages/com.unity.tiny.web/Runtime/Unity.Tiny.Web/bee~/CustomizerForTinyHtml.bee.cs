using System;
using Unity.BuildSystem.NativeProgramSupport;

class CustomizerForTinyHTML : AsmDefCSharpProgramCustomizer
{
    public override string CustomizerFor => "Unity.Tiny.Web";

    public override string[] ImplementationFor => new[] {"Unity.Tiny.Core"};

    public override void CustomizeSelf(AsmDefCSharpProgram program)
    {
        Il2Cpp.AddLibIl2CppAsLibraryFor(program.NativeProgram);
    }
}
