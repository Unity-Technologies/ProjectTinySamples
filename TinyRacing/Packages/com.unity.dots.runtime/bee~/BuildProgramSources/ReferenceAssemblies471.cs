using Bee.DotNet;
using NiceIO;
using System;
using Unity.BuildSystem.CSharpSupport;

class ReferenceAssemblies471
{
    public static NPath[] Paths => _paths.Value;

    static readonly Lazy<NPath[]> _paths = new Lazy<NPath[]>(() =>
    {
        ReferenceAssemblyProvider.Default.TryFor(Framework.Framework471, true, out NPath[] referenceAssemblies, out _);
        return referenceAssemblies;
    });
}