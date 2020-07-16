using System;
using UnityEditor;

// ReSharper disable InconsistentNaming

#pragma warning disable 649
#pragma warning disable 414

namespace Unity.Entities.Runtime.Build
{
    // The analytics data types
    // Any changes in here requires a schema change!
    //
    // Note to contributors: Use snake_case for serialized fields sent as part of event payloads
    // this convention is used by the Data Science team

    static class BuildAnalyticsTypes
    {
        internal static string EventVendorKey = "unity.dotsruntime";

        internal enum EventName
        {
            dotsRuntime,
            dotsRuntimeBuild
        }

        internal enum EventCategory
        {
            Custom = 0,
            Information = 1,
            Warning = 2,
            Error = 3,
            Usage = 4
        }

        [Serializable]
        internal struct ContextInfo
        {
            public bool internal_build;
            public string platform;
            public string configuration;
            public bool run;
        }

        [Serializable]
        internal struct ProjectInfo
        {
            public string[] modules;
        }

        [Serializable]
        internal struct PackageVersion
        {
            public string version;
            public bool preview;
            public bool embedded;

            public static PackageVersion For(string packageName)
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath($"Packages/{packageName}");
                if (package == null)
                {
                    return new PackageVersion() { version = "", preview = false, embedded = false };
                }

                return new PackageVersion()
                {
                    version = package.version,
                    embedded = package.source == UnityEditor.PackageManager.PackageSource.Embedded,
                    preview = package.version.Contains("preview"),
                };
            }
        }

        [Serializable]
        internal struct AnalyticsEventCommon
        {
            public PackageVersion dotsRuntimePackageVersion;
            public PackageVersion entitiesPackageVersion;
            public PackageVersion tinyPackageVersion;
            public ContextInfo context;
            public ProjectInfo project;
        }

        [Serializable]
        internal struct GenericEvent
        {
            public AnalyticsEventCommon common;

            public string category;
            public int category_id;
            public string name;
            public string message;
            public string description;
            public long duration;
        }

        [Serializable]
        internal struct BuildEvent
        {
            public AnalyticsEventCommon common;

            public long duration;
            public bool success;
        }
    }
}
