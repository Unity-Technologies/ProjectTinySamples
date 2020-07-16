using System;
using System.Linq;

public abstract class DotsRuntimeCSharpProgramCustomizer
{
    protected static DotsRuntimeCSharpProgramCustomizer[] All = MakeAllCustomizers();

    static DotsRuntimeCSharpProgramCustomizer[] MakeAllCustomizers()
    {
        return typeof(BuildProgram).Assembly
            .GetTypes()
            .Where(t => typeof(DotsRuntimeCSharpProgramCustomizer).IsAssignableFrom(t))
            .Where(t=>!t.IsAbstract)
            .OrderBy(t => t.Name)
            .Select(Activator.CreateInstance)
            .Cast<DotsRuntimeCSharpProgramCustomizer>()
            .ToArray();
    }

    public static void RunAllCustomizersOn(DotsRuntimeCSharpProgram program)
    {
        foreach(var customizer in All)
            customizer.Customize(program);
    }

    public static DotsRuntimeCSharpProgram RunAllCustomizersTryCreateProgramForAsmDef(AsmDefDescription asmDef)
    {
        DotsRuntimeCSharpProgram result = null;
        Type resultCustomizer = null;
        foreach (var customizer in All)
        {
            var r = customizer.TryCreateProgramForAsmDef(asmDef);
            if (r == null)
                continue;
            if (result != null)
            {
                throw new InvalidOperationException(
                    $"Both {customizer.GetType()} and {resultCustomizer} created a custom CSharpProgram for {asmDef.Name}");
            }

            result = r;
            resultCustomizer = customizer.GetType();
        }

        return result;
    }

    public abstract void Customize(DotsRuntimeCSharpProgram program);

    public virtual DotsRuntimeCSharpProgram TryCreateProgramForAsmDef(AsmDefDescription asmDef)
    {
        return null;
    }
}