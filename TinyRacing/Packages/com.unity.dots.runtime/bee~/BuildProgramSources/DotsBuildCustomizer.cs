using System;
using System.Linq;

public abstract class DotsBuildCustomizer
{
    protected static DotsBuildCustomizer[] All = MakeAllCustomizers();

    static DotsBuildCustomizer[] MakeAllCustomizers()
    {
        return typeof(BuildProgram).Assembly
            .GetTypes()
            .Where(t => typeof(DotsBuildCustomizer).IsAssignableFrom(t))
            .Where(t=>!t.IsAbstract)
            .OrderBy(t => t.Name)
            .Select(Activator.CreateInstance)
            .Cast<DotsBuildCustomizer>()
            .ToArray();
    }

    public static void RunAllCustomizers()
    {
        foreach(var customizer in All)
            customizer.Customize();
    }

    public abstract void Customize();
}
