using JetBrains.Annotations;

[UsedImplicitly]
class CustomizerForTinyAudioHTML : AsmDefCSharpProgramCustomizer
{
    public override string CustomizerFor => "Unity.Tiny.Audio.Web";

    public override string[] ImplementationFor => new [] { "Unity.Tiny.Audio" };
}
