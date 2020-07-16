using Unity.Build;
using Unity.Build.DotsRuntime;
using Unity.Properties;
using Unity.Serialization.Json;

namespace Unity.Entities.Runtime.Build
{
    public sealed class DotsRuntimeBurstSettings : IDotsRuntimeBuildModifier
    {
        [CreateProperty]
        public bool EnableBurst { get; set; } = true;

        public void Modify(JsonObject jsonObject)
        {
            jsonObject["EnableBurst"] = EnableBurst;
        }
    }
}
