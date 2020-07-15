using JetBrains.Annotations;

[UsedImplicitly]
class CustomizerForImage2DWeb : AsmDefCSharpProgramCustomizer
{
    public override string CustomizerFor => "Unity.Tiny.Image2D.Web";

    public override string[] ImplementationFor => new [] { "Unity.Tiny.Image2D" };
}
