using System;
using System.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NUnit.Framework
{
    // If an assertion is [NotSupported], the test won't run.
    sealed class NotSupportedAttribute : Attribute
    {
        public NotSupportedAttribute(string _)
        {
        }
    }

    // If an assertion is [PartiallySupported], the test will run,
    // but the assert will not be checked.
    sealed class PartiallySupportedAttribute : Attribute
    {
        public PartiallySupportedAttribute(string _)
        {
        }
    }

    public static class Assert
    {
        // These values are set by code-gen!
        public static ulong assertPassCount;
        public static int testsRan;
        public static int testsIgnored;
        public static int testsLimitation;
        public static int testsNotSupported;
        public static int testsPartiallySupported;

        static internal void LogExpectedAndThrow<T>(string msg, T a, T b)
        {
            Console.WriteLine(msg);
            Console.WriteLine($"Expected: {a}");
            Console.WriteLine($"Actual:   {b}");
            throw new Exception(msg);
        }

        static internal void LogAndThrow<T>(string msg, T a, T b)
        {
            Console.WriteLine(msg);
            Console.WriteLine($"a: {a}");
            Console.WriteLine($"b: {b}");
            throw new Exception(msg);
        }

        public static void IsTrue(bool value, string msg = "")
        {
            if (value)
            {
                ++assertPassCount;
                return;
            }
            Console.WriteLine("Assert.IsTrue " + msg);
            throw new Exception("Assert.IsTrue " + msg);
        }

        public static void IsFalse(bool value, string msg = "")
        {
            if (!value)
            {
                ++assertPassCount;
                return;
            }
            Console.WriteLine("Assert.IsFalse " + msg);
            throw new Exception("Assert.IsFalse " + msg);
        }

        public static void True(bool value, string msg = "")
        {
            if (value)
            {
                ++assertPassCount;
                return;
            }
            Console.WriteLine("Assert.True " + msg);
            throw new Exception("Assert.True " + msg);
        }

        public static void False(bool value, string msg = "")
        {
            if (!value)
            {
                ++assertPassCount;
                return;
            }
            Console.WriteLine("Assert.False " + msg);
            throw new Exception("Assert.False " + msg);
        }

        public static void AreEqual(int a, int b, string msg = "")
        {
            if (a == b)
            {
                ++assertPassCount;
                return;
            }
            LogExpectedAndThrow("Assert.AreEqual(int, int) " + msg, a, b);
        }

        public static void AreEqual(long a, long b, string msg = "")
        {
            if (a == b)
            {
                ++assertPassCount;
                return;
            }
            LogExpectedAndThrow("Assert.AreEqual(long, long) " + msg, a, b);
        }

        public static void AreNotEqual(int a, int b, string msg = "")
        {
            if (a != b)
            {
                ++assertPassCount;
                return;
            }

            LogExpectedAndThrow("Assert.AreNotEqual(int, int) " + msg, a, b);
        }

        public static void AreEqual(float a, float b, string msg = "")
        {
            if (a == b)
            {
                ++assertPassCount;
                return;
            }
            LogExpectedAndThrow("Assert.AreEqual(float, float) " + msg, a, b);
        }

        public static void AreEqual(double a, double b, float err, string msg = "")
        {
            if (Math.Abs(a - b) <= err)
            {
                ++assertPassCount;
                return;
            }
            LogExpectedAndThrow("Assert.AreEqual(double, double) " + msg, a, b);
        }

        public static void AreEqual(uint a, uint b, string msg = "")
        {
            if (a == b)
            {
                ++assertPassCount;
                return;
            }
            LogExpectedAndThrow("Assert.AreEqual(uint, uint) " + msg, a, b);
        }

        public static void AreNotEqual(uint a, uint b, string msg = "")
        {
            if (a != b)
            {
                ++assertPassCount;
                return;
            }
            LogExpectedAndThrow("Assert.AreNotEqual(uint, uint) " + msg, a, b);
        }

        public static void AreEqual(Type a, Type b, string msg = "")
        {
            if (a == b)
            {
                ++assertPassCount;
                return;
            }
            LogExpectedAndThrow("Assert.AreEqual(Type, Type) " + msg, a, b);
        }

        public static unsafe void AreEqual<T>(T[] a, T[] b, string msg = "") where T : struct
        {
            if (a.Length != b.Length)
            {
                LogExpectedAndThrow("Assert.AreEqual([], [] " + msg, a.Length, b.Length);
            }

            for (int i = 0; i < a.Length; ++i)
            {
                T ta = a[i];
                T tb = b[i];
                if (UnsafeUtility.MemCmp(UnsafeUtility.AddressOf(ref ta), UnsafeUtility.AddressOf(ref tb), UnsafeUtility.SizeOf<T>()) != 0)
                {
                    Console.WriteLine($"Error at index: {i}");
                    LogExpectedAndThrow("Assert.AreEqual([], []) " + msg, a[i], b[i]);
                }
            }

            ++assertPassCount;
        }

        public static unsafe void AreEqual<T>(T a, T b, string msg = "") where T : struct
        {
            if (UnsafeUtility.MemCmp(UnsafeUtility.AddressOf(ref a), UnsafeUtility.AddressOf(ref b), UnsafeUtility.SizeOf<T>()) == 0)
            {
                ++assertPassCount;
                return;
            }

            Console.WriteLine("Assert.AreEqual(struct, struct) " + msg);
            throw new Exception("Assert.AreEqual(struct, struct) " + msg);
        }

        public static unsafe void AreNotEqual<T>(T a, T b, string msg = "") where T : struct
        {
            if (UnsafeUtility.MemCmp(UnsafeUtility.AddressOf(ref a), UnsafeUtility.AddressOf(ref b), UnsafeUtility.SizeOf<T>()) != 0)
            {
                ++assertPassCount;
                return;
            }

            Console.WriteLine("Assert.AreNotEqual(struct, struct) " + msg);
            throw new Exception("Assert.AreNotEqual(struct, struct) " + msg);
        }

        public static void AreEqual(string a, string b, string msg = "")
        {
            if (a == b)
            {
                ++assertPassCount;
                return;
            }
            LogExpectedAndThrow("Assert.AreEqual(string, string) " + msg, a, b);
        }

        public static void LessOrEqual(int a, int b, string msg = "")
        {
            if (a <= b)
            {
                ++assertPassCount;
                return;
            }
            LogAndThrow("Assert.LessOrEqual " + msg, a, b);
        }

        public static void Less(int a, int b, string msg = "")
        {
            if (a < b)
            {
                ++assertPassCount;
                return;
            }
            LogAndThrow("Assert.Less " + msg, a, b);
        }

        public static void Greater(int a, int b, string msg = "")
        {
            if (a > b)
            {
                ++assertPassCount;
                return;
            }
            LogAndThrow("Assert.Greater(int, int) " + msg, a, b);
        }

        public static void Greater(ulong a, ulong b, string msg = "")
        {
            if (a > b)
            {
                ++assertPassCount;
                return;
            }
            LogAndThrow("Assert.Greater(ulong, ulong) " + msg, a, b);
        }

        public static void GreaterOrEqual(int a, int b, string msg = "")
        {
            if (a >= b)
            {
                ++assertPassCount;
                return;
            }
            LogAndThrow("Assert.GreaterOrEqual(int, int) " + msg, a, b);
        }

        [PartiallySupported("Assert.AreEqual(Entity[] a, Entity[] b are not validated")]
        public static void AreEqual(Entity[] a, Entity[] b)
        {
        }

        // very minory issue: AreEqual(object, object) and related can probably be "promoted" to
        // PartiallySupported. But it's better to just fix the test cases. Very few tests call this.
        [NotSupported("Assert.AreEqual(object, object)")]
        public static void AreEqual(object a, object b)
        {
        }

        [NotSupported("Assert.Contains(object, object)")]
        public static void Contains(object a, object b)
        {
            throw new Exception("Should be replaced by code-gen");
        }

        [PartiallySupported("Assert.Throws are not validated")]
        public static void Throws<T>(TestDelegate _) where T : Exception
        {
        }

        [NotSupported("Assert.AreNotEqual(object, object)")]
        public static void AreNotEqual(object a, object b)
        {
        }

        public static void AreSame(object a, object b)
        {
            if (ReferenceEquals(a, b))
            {
                ++assertPassCount;
                return;
            }
            LogAndThrow("Assert.AreSame", a, b);
        }

        public static void AreNotSame(object a, object b)
        {
            if (!ReferenceEquals(a, b))
            {
                ++assertPassCount;
                return;
            }
            LogAndThrow("Assert.AreNotSame", a, b);
        }

        public static void DoesNotThrow(TestDelegate code)
        {
            // Interestingly...we just need to run the code. If it *does* throw,
            // then test case just fails.
            ++assertPassCount;
            code();
        }

        public static void Fail(string msg = "")
        {
            Console.WriteLine("Assert.Fail() " + msg);
            throw new Exception("Assert.Fail() " + msg);
        }

        public static void Ignore(string msg)
        {
            // This is a real API.
            // We...ignore it.
        }

        public static void IsNull<T>(T t)
        {
            if (t == null)
            {
                ++assertPassCount;
                return;
            }
            Console.WriteLine("Assert.IsNull()");
            throw new Exception("Assert.IsNull()");
        }

        public static void NotNull<T>(T t)
        {
            if (t != null)
            {
                ++assertPassCount;
                return;
            }
            Console.WriteLine("Assert.NotNull()");
            throw new Exception("Assert.NotNull()");
        }

        // This is a very simple & partial usage of That
        // to keep from re-implementing a ton of code.
        public static void That(int a, int b)
        {
            if (a == b)
            {
                ++assertPassCount;
                return;
            }

            LogAndThrow("Assert.That(int, int)", a, b);
        }

        public static void That(uint a, uint b)
        {
            if (a == b)
            {
                ++assertPassCount;
                return;
            }

            LogAndThrow("Assert.That(uint, uint)", a, b);
        }

        public static void That(ulong a, ulong b)
        {
            if (a == b)
            {
                ++assertPassCount;
                return;
            }

            LogAndThrow("Assert.That(ulong, ulong)", a, b);
        }

        public static void That(string a, string b)
        {
            if (a == b)
            {
                ++assertPassCount;
                return;
            }

            LogAndThrow("Assert.That(string, string)", a, b);
        }

        public static void That(bool a, bool b)
        {
            if (a == b)
            {
                ++assertPassCount;
                return;
            }

            LogAndThrow("Assert.That(bool, bool)", a, b);
        }

        [PartiallySupported("Assert.That unhandled parameters are not validated")]
        public static void That(params object[] _)
        {
        }
    }

    // Very partial & simple!
    public static class Is
    {
        public static T EqualTo<T>(T a)
        {
            return a;
        }

        public static bool True => true;
        public static bool False => false;

        public static object Null => null;

        [NotSupported("Is.EquivalentTo")]
        public static T EquivalentTo<T>(T a)
        {
            throw new Exception("Is.EquivalentTo");
        }

        [NotSupported("Is.InstanceOf")]
        public static bool InstanceOf<T>(T a)
        {
            throw new Exception("Is.InstanceOf");
        }
    }

    public static class CollectionAssert
    {
        [PartiallySupported("CollectionAssert.AreEquivalent are not validated")]
        public static void AreEquivalent(IEnumerable expected, IEnumerable actual)
        {
        }

        [PartiallySupported("CollectionAssert.AreEqual are not validated")]
        public static void AreEqual(IEnumerable expected, IEnumerable actual)
        {
        }

        [PartiallySupported("CollectionAssert.Contains are not validated")]
        public static void Contains(IEnumerable a, object b)
        {
        }

        [PartiallySupported("CollectionAssert.DoesNotContain are not validated")]
        public static void DoesNotContain(IEnumerable a, object b)
        {
        }

        public static void IsEmpty(IEnumerable it)
        {
            foreach (var i in it)
            {
                Console.WriteLine("CollectionAssert.IsEmpty()");
                throw new Exception("CollectionAssert.IsEmpty()");
            }
            ++Assert.assertPassCount;
        }
    }
}
