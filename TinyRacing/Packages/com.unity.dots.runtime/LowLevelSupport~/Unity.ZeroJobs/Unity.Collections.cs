using System;
using System.Runtime.InteropServices;
#if !NET_DOTS
using System.Text.RegularExpressions;
#endif
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Collections
{
    public class NativeContainerIsAtomicWriteOnlyAttribute : Attribute {}
    public class ReadOnlyAttribute : Attribute {}
    public class WriteOnlyAttribute : Attribute {}
    public class NativeDisableParallelForRestrictionAttribute : Attribute {}
    public class DeallocateOnJobCompletionAttribute : Attribute {}
    public class WriteAccessRequiredAttribute : Attribute {}
}

namespace Unity.Collections.LowLevel.Unsafe
{
    public class NativeSetThreadIndexAttribute : Attribute {}
    public sealed class NativeContainerAttribute : Attribute {}
    public class NativeDisableUnsafePtrRestrictionAttribute : Attribute {}
    public sealed class NativeContainerSupportsMinMaxWriteRestriction : Attribute {}
    public class NativeContainerSupportsDeallocateOnJobCompletionAttribute : Attribute {}
    public class NativeContainerSupportsDeferredConvertListToArray : Attribute {}
    public class NativeSetClassTypeToNullOnSchedule : Attribute {}
    public class NativeContainerIsReadOnly : Attribute {}
    public sealed class NativeDisableContainerSafetyRestrictionAttribute : Attribute {}
}

