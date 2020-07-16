using System;

namespace Unity.Collections.LowLevel.Unsafe
{
    public static partial class UnsafeUtility
    {
        
        /*
        // just to hide .net API differences
        private static bool IsValueType(Type t)
        {
        #if ENABLE_DOTNET
            return t.GetTypeInfo().IsValueType;
        #else
            return t.IsValueType;
        #endif
        }

        private static bool IsPrimitive(Type t)
        {
        #if ENABLE_DOTNET
            return t.GetTypeInfo().IsPrimitive;
        #else
            return t.IsPrimitive;
        #endif
        }

        private static bool IsBlittableValueType(Type t) { return IsValueType(t) && IsBlittable(t); }

        private static string GetReasonForTypeNonBlittableImpl(Type t, string name)
        {
            return "UnknownReason";
        }

        // while it would make sense to have functions like ThrowIfArgumentIsNonBlittable
        // currently we insist on including part of call stack into exception message in these cases
        // e.g. "T used in NativeArray<T> must be blittable"
        // due to that we will need to pass message string to this function
        //   but most of the time we will be creating it using string.Format and it will happen on every check
        //   instead of "only if we fail check for is-blittable"
        // thats why we provide the means to implement this pattern on your code (but not function itself)

        
        internal static bool IsArrayBlittable(Array arr)
        {
            return IsBlittableValueType(arr.GetType().GetElementType());
        }

        internal static bool IsGenericListBlittable<T>() where T : struct
        {
            return IsBlittable<T>();
        }

        internal static string GetReasonForArrayNonBlittable(Array arr) => "Unknown";

        internal static string GetReasonForGenericListNonBlittable<T>() where T : struct => "Unknown";

        internal static string GetReasonForTypeNonBlittable(Type t) => "Unknown";
*/
        public static string GetReasonForValueTypeNonBlittable<T>() where T : struct
        {
            return "Unknown";
        }

        public static bool IsBlittable(Type type)
        {
            return true;
        }

        // TODO -- il2cpp compile-time intrinsics
        // or UnityLinker replacement?
        public static unsafe bool IsBlittable<T>()
        {
            return true;
        }

        public static bool IsUnmanaged<T>()
        {
            return true;
        }

        public static bool IsValidNativeContainerElementType<T>()
        {
            return true;
        }
    }
}
