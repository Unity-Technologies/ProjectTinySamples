using System;
using System.Collections.Generic;
using System.Linq;

internal abstract class AsmDefCSharpProgramCustomizer : DotsRuntimeCSharpProgramCustomizer
{
    protected new static IEnumerable<AsmDefCSharpProgramCustomizer> All =>
        DotsRuntimeCSharpProgramCustomizer.All.OfType<AsmDefCSharpProgramCustomizer>();
    
    public static void RunAllAddPlatformImplementationReferences(AsmDefCSharpProgram aprogram)
    {
        foreach (var customizer in All)
            customizer.AddPlatformImplementationReferences(aprogram);
    } 
    
    /// <summary>
    /// The name of the asmdef to which this customizer should apply.
    /// </summary>
    public abstract string CustomizerFor { get; }

    /// <summary>
    /// An array of asmdef names for which this customizer's asmdef is a platform implementation.
    /// </summary>
    public virtual string[] ImplementationFor { get; } = Array.Empty<string>();

    /// <summary>
    /// Called to customize the program when the program matches the asmdef for which
    /// this is a customizer for.
    /// </summary>
    /// <param name="aprogram">The program</param>
    public virtual void CustomizeSelf(AsmDefCSharpProgram aprogram)
    {
    }
   
    /// <summary>
    /// Called to customize the program when the program does not matche the asmdef for which
    /// this is a customizer for.
    /// </summary>
    public virtual void CustomizeOther(AsmDefCSharpProgram aprogram)
    {
    }

    protected string[] _implForCache;
    
    public override void Customize(DotsRuntimeCSharpProgram program)
    {
        var aprogram = program as AsmDefCSharpProgram;
        if (aprogram == null)
            return;

        if (aprogram.AsmDefDescription.Name == CustomizerFor)
        {
            CustomizeSelf(aprogram);
        }
        else
        {
            CustomizeOther(aprogram);
        }
    }

    public void AddPlatformImplementationReferences(AsmDefCSharpProgram aprogram)
    {
        if (_implForCache == null)
            _implForCache = ImplementationFor;

        foreach (var src in _implForCache)
            aprogram.AddPlatformImplementationFor(src, CustomizerFor);
    }
}
