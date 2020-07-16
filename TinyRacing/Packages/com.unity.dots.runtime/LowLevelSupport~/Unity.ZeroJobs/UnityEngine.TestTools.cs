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

namespace UnityEngine.TestTools
{
    public static class LogAssert
    {
        public static void Expect(LogType type, string message)
        {
            if (type == LogType.Log) {
                if (!message.Equals(Debug.lastLog))
                    throw new InvalidOperationException();
            } else if (type == LogType.Warning) {
                if (!message.Equals(Debug.lastWarning))
                    throw new InvalidOperationException();
            }
        }
#if !NET_DOTS
        public static void Expect(LogType type, Regex message)
        {
            if (type == LogType.Log) {
                if (!message.Match(Debug.lastLog).Success)
                    throw new InvalidOperationException();
            } else if (type == LogType.Warning) {
                if (!message.Match(Debug.lastWarning).Success)
                    throw new InvalidOperationException();
            }
        }
#endif
        public static void NoUnexpectedReceived()
        {
        }
    }
}
