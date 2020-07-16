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

namespace UnityEngine.Scripting
{
    public class PreserveAttribute : Attribute {}
}
