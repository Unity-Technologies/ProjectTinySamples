using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Build;
using UnityEditor;
using UnityEngine;
using UnityEngine.Analytics;

namespace Unity.Entities.Runtime.Build
{
    [InitializeOnLoad]
    internal static class BuildEditorAnalytics
    {
        static bool s_Enabled;
        static bool s_RegistrationDone;

        static BuildAnalyticsTypes.PackageVersion s_TinyPackageVersion;
        static BuildAnalyticsTypes.PackageVersion s_EntitiesPackageVersion;
        static BuildAnalyticsTypes.PackageVersion s_DotsRuntimePackageVersion;

        static readonly string[] PackageNamesForExceptionReporting = new[]
        {
            "com.unity.dots.runtime",
            "com.unity.tiny",
        };

        static readonly HashSet<int> s_OnceHashCodes = new HashSet<int>();

        static BuildEditorAnalytics()
        {
            EditorApplication.delayCall += () =>
            {
                s_TinyPackageVersion = BuildAnalyticsTypes.PackageVersion.For("com.unity.tiny");
                s_EntitiesPackageVersion = BuildAnalyticsTypes.PackageVersion.For("com.unity.entities");
                s_DotsRuntimePackageVersion = BuildAnalyticsTypes.PackageVersion.For("com.unity.dots.runtime");

                // if the exception trace has anything from one of our the packages that we
                // want to report exceptions for, do so
                Application.logMessageReceived += (condition, trace, type) =>
                {
                    if (type == LogType.Exception &&
                        !string.IsNullOrEmpty(trace) &&
                        PackageNamesForExceptionReporting.Any(pkg => trace.Contains(pkg)))
                    {
                        if (s_OnceHashCodes.Add(trace.GetHashCode()))
                        {
                            // TODO sanitize 'trace' to remove personal filenames and such
                            SendErrorEvent("__uncaught__", condition /*, trace*/);
                        }
                    }
                };
            };
        }

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            BuildProcess.BuildCompleted += (result) => SendBuildEvent(result);
        }

        static bool RegisterEvents()
        {
            if (s_RegistrationDone)
                return s_Enabled;

            s_RegistrationDone = true;
            s_Enabled = false;

            if (UnityEditorInternal.InternalEditorUtility.inBatchMode)
                return false;


            foreach (var eventName in Enum.GetNames(typeof(BuildAnalyticsTypes.EventName)))
            {
                if (!RegisterEvent(eventName))
                    return false;
            }

            s_Enabled = true;
            return true;
        }

        static bool RegisterEvent(string eventName)
        {
            var result = EditorAnalytics.RegisterEventWithLimit(eventName, 100, 1000, BuildAnalyticsTypes.EventVendorKey);
            switch (result)
            {
                case AnalyticsResult.Ok:
                    return true;
                case AnalyticsResult.TooManyRequests:
                    // this is fine - event registration survives domain reload (native)
                    return true;
                case AnalyticsResult.AnalyticsDisabled:
                    return false;
                default:
                    TraceError($"failed to register analytics event '{eventName}'. Result: '{result}'");
                    return false;
            }
        }

        static void TraceError(string message)
        {
            message = "DotsRuntimeBuildAnalytics: " + message;
            Console.WriteLine(message);
        }

        static BuildAnalyticsTypes.AnalyticsEventCommon CreateCommon(BuildResult result = null)
        {
            return new BuildAnalyticsTypes.AnalyticsEventCommon()
            {
                dotsRuntimePackageVersion = s_DotsRuntimePackageVersion,
                entitiesPackageVersion = s_EntitiesPackageVersion,
                tinyPackageVersion = s_TinyPackageVersion,
                context = CreateContextInfo(result),
                project = CreateProjectInfo(result),
            };
        }

        static BuildAnalyticsTypes.ContextInfo CreateContextInfo(BuildResult result)
        {
            var ci = new BuildAnalyticsTypes.ContextInfo();
#if UNITY_INTERNAL
            ci.internal_build = true;
#else
            ci.internal_build = Unsupported.IsDeveloperMode();
#endif

            if (result == null)
                return ci;

            if (!result.BuildConfiguration.TryGetComponent<DotsRuntimeBuildProfile>(out var profile))
                return ci;

            ci.configuration = profile.Configuration.ToString();
            ci.platform = profile.Target.UnityPlatformName;
            return ci;
        }

        static BuildAnalyticsTypes.ProjectInfo CreateProjectInfo(BuildResult result)
        {
            if (result == null)
                return default;

            if (!result.BuildConfiguration.TryGetComponent<DotsRuntimeBuildProfile>(out var profile))
                return default;

            var unityAssembliesInProject = new List<string>();

            foreach (var assembly in profile.TypeCache.Assemblies)
            {
                // find the asset that corresponds to the thing that was built
                var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);
                if (pkg == null)
                    continue;
                if (!pkg.name.StartsWith("com.unity"))
                    continue;
                unityAssembliesInProject.Add(assembly.GetName().Name);
            }

            return new BuildAnalyticsTypes.ProjectInfo { modules = unityAssembliesInProject.ToArray() };
        }

        internal static void SendCustomEvent(string category, string name, string message = null, string description = null)
        {
            SendEvent(BuildAnalyticsTypes.EventCategory.Custom, category, name, message, description, TimeSpan.Zero);
        }

        internal static void SendCustomEvent(string category, string name, TimeSpan duration, string message = null, string description = null)
        {
            SendEvent(BuildAnalyticsTypes.EventCategory.Custom, category, name, message, description, duration);
        }

        internal static void SendExceptionOnce(string name, Exception ex)
        {
            if (ex == null)
            {
                return;
            }
            var hashCode = ex.StackTrace.GetHashCode();
            if (s_OnceHashCodes.Add(hashCode))
            {
                SendException(name, ex);
            }
        }

        internal static void SendException(string name, Exception ex)
        {
            if (ex == null)
            {
                return;
            }
            // we can't send ex.ToString() because it might contain user info
            // need to pull out the parts of the exception relevant to us,
            // and sanitize filenames and all that.
            // TODO sanitize 'ex.ToString()' to remove personal filenames and such
            SendErrorEvent(name, ex.Message); //, ex.ToString());
        }

        internal static void SendErrorEvent(string name, string message = null, string description = null)
        {
            SendEvent(BuildAnalyticsTypes.EventCategory.Error, name, TimeSpan.Zero, message, description);
        }

        internal static void SendEvent(BuildAnalyticsTypes.EventCategory category, string name, string message = null, string description = null)
        {
            SendEvent(category, category.ToString(), name, message, description, TimeSpan.Zero);
        }

        internal static void SendEvent(BuildAnalyticsTypes.EventCategory category, string name, TimeSpan duration, string message = null, string description = null)
        {
            SendEvent(category, category.ToString(), name, message, description, duration);
        }

        static void SendEvent(BuildAnalyticsTypes.EventCategory category, string categoryName, string name, string message, string description,
            TimeSpan duration)
        {
            if (string.IsNullOrEmpty(categoryName) || string.IsNullOrEmpty(name))
            {
                TraceError(new ArgumentNullException().ToString());
                return;
            }
            var e = new BuildAnalyticsTypes.GenericEvent()
            {
                common = CreateCommon(),

                category = categoryName,
                category_id = (int)category,
                name = name,
                message = message,
                description = description,
                duration = duration.Ticks
            };

            Send(BuildAnalyticsTypes.EventName.dotsRuntime, e);
        }

        internal static void SendBuildEvent(BuildResult result)
        {
            if (!result.BuildConfiguration.HasComponent<DotsRuntimeBuildProfile>())
                return;

            var e = new BuildAnalyticsTypes.BuildEvent()
            {
                common = CreateCommon(result),

                duration = result.Duration.Ticks,
                success = result.Succeeded,
            };

            Send(BuildAnalyticsTypes.EventName.dotsRuntimeBuild, e);
        }

        static void Send(BuildAnalyticsTypes.EventName eventName, object eventData)
        {
            if (!RegisterEvents())
                return;

            var result = EditorAnalytics.SendEventWithLimit(eventName.ToString(), eventData);
            if (result == AnalyticsResult.Ok)
            {
                if (UnityEditor.Unsupported.IsDeveloperMode())
                {
                    Debug.Log($"DotsRuntimeBuildAnalytics: event='{eventName}', time='{DateTime.Now:HH:mm:ss}', payload={EditorJsonUtility.ToJson(eventData, true)}");
                }
            }
            else
            {
                TraceError($"failed to send event {eventName}. Result: {result}");
            }
        }
    }
}
