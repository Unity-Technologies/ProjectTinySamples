using System;
using Unity.Entities.Runtime.Hashing;

namespace Unity.Entities.Runtime
{
    public static class ConfigurationScene
    {
        public static readonly string Path = "Configuration";
        public static readonly Guid Guid = GuidUtility.NewGuid(Path);
    }
}
