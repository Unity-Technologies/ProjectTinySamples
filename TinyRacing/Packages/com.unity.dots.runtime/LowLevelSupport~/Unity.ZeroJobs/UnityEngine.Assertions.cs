using System;
using System.Runtime.InteropServices;
using Unity.Burst;
#if !NET_DOTS
using System.Text.RegularExpressions;
#endif
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Assertions;

namespace UnityEngine.Assertions
{
    public static class Assert
    {
        [BurstDiscard]
        static unsafe void Failure()
        {
            // Take assertion at "real value": an Assert should *never* fire.
            // If in debug w/ GUARD_HEAP (the default) AssertHeap will crash the program (with a call stack in the debugger.)
            // As a fallback, throw an exception. Note that so much code catches exceptions this can hide Asserts.
            UnsafeUtility.AssertHeap(null);
            throw new Exception("Exception caused by internal assert.");
        }


        [BurstDiscard]
        public static void AreEqual(object one, object two)
        {
            if (!one.Equals(two)) Failure();
        }

        [BurstDiscard]
        public static void AreEqual(int one, int two)
        {
            if (one != two) Failure();
        }
        
        [BurstDiscard]
        public static void AreEqual(float one, float two)
        {
        }

        [BurstDiscard]
        public static void AreNotEqual(object one, object two)
        {
            if (one.Equals(two)) Failure();
        }
        
        [BurstDiscard]
        public static void AreNotEqual(int one, int two)
        {
            if (one == two) Failure();
        }

        [BurstDiscard]
        public static void AreNotEqual(float one, float two)
        {
            if (one == two) Failure();
        }

        [BurstDiscard]
        public static void IsTrue(bool b, string msg = null)
        {
            if (!b) Failure();
        }

        [BurstDiscard]
        public static void IsFalse(bool b, string msg = null)
        {
            if (b) Failure();
        }

        [BurstDiscard]
        public static void AreApproximatelyEqual(object one, object two, object three = null, object msg =null)
        {
        }
    }
}
