using JetBrains.Annotations;

[UsedImplicitly]
class CustomizerForInputHTML : AsmDefCSharpProgramCustomizer
{
    public override string CustomizerFor => "Unity.Tiny.Input.Web";

    public override string[] ImplementationFor => new [] { "Unity.Tiny.Input" };
}
