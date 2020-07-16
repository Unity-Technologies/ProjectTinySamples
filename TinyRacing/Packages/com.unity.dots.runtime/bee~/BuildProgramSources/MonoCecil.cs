using System;
using System.Collections.Generic;
using Bee.Core;
using Bee.Stevedore;
using NiceIO;
static class MonoCecil
{
    public static NPath[] Paths => _paths.Value;

    static readonly Lazy<NPath[]> _paths = new Lazy<NPath[]>(() =>
    {
        var packageCachePath = AsmDefConfigFile.UnityProjectPath.Combine("Library", "PackageCache");
        var cecilPaths = packageCachePath.Directories("nuget.mono-cecil@*");
        if (cecilPaths == null || cecilPaths.Length == 0)
            throw new Exception("DOTS Runtime requires a package reference to 'nuget.mono-cecil' in the project's 'manifest.json' file.");

        // Just a path search. The user should really only have one version lingering here, and a path search should handle most cases
        // However, versions can get wacky, so we may need a more intelligent version string sort in the future.
        Array.Sort(cecilPaths);
        var latestCecilPath = cecilPaths[cecilPaths.Length-1];

        return new[]
        {
            latestCecilPath.Combine("Mono.Cecil.dll"),
            latestCecilPath.Combine("Mono.Cecil.Rocks.dll"),
            latestCecilPath.Combine("Mono.Cecil.Mdb.dll"),
            latestCecilPath.Combine("Mono.Cecil.Pdb.dll")
        };
    });
}