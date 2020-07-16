using System;
using System.Collections.Generic;
using UnityEditor;

namespace Unity.Entities.Runtime.Build
{
    internal static partial class ConversionUtils
    {
        public static string GetScenePathForSceneWithName(string name)
        {
            foreach (var sceneGuid in AssetDatabase.FindAssets($"{name} t:scene"))
            {
                var path = AssetDatabase.GUIDToAssetPath(sceneGuid);
                if (path.EndsWith($"{name}.unity"))
                    return path;
            }

            throw new InvalidOperationException($"Can't find scene named {name}.unity in project");
        }
    }

    internal static class EnumerableExtensions
    {
        public static IEnumerable<T> ToSingleEnumerable<T>(this T obj)
        {
            yield return obj;
        }
    }
}
