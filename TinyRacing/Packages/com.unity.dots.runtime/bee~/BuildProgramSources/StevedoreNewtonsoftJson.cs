using System;
using Bee.Core;
using Bee.Stevedore;
using NiceIO;

static class StevedoreNewtonsoftJson
{
    public static NPath[] Paths => _paths.Value;
    
    static readonly Lazy<NPath[]> _paths = new Lazy<NPath[]>(() =>
    {
        var newtonsoftJsonArtifact = new StevedoreArtifact("newtonsoft-json");
        Backend.Current.Register(newtonsoftJsonArtifact);

        return new[]
        {
            newtonsoftJsonArtifact.Path.Combine("lib", "net40", "Newtonsoft.Json.dll"),
        };
    });
}
