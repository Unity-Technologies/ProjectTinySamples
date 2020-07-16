using Unity.Serialization.Json;
using Unity.Serialization.Json.Adapters;
using UnityEditor;
using BuildTarget = Unity.Platforms.BuildTarget;

namespace Unity.Entities.Runtime.Build
{
    class DotsRuntimeBuildProfileJsonAdapter : IJsonAdapter<BuildTarget>
    {
        [InitializeOnLoadMethod]
        static void Initialize()
        {
            JsonSerialization.AddGlobalAdapter(new DotsRuntimeBuildProfileJsonAdapter());
        }

        public void Serialize(JsonStringBuffer writer, BuildTarget value)
        {
            writer.WriteEncodedJsonString(value?.BeeTargetName);
        }

        public BuildTarget Deserialize(SerializedValueView view)
        {
            return BuildTarget.GetBuildTargetFromBeeTargetName(view.ToString());
        }
    }
}
