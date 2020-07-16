# Portable Test Runner

This document is an overview of the portable, cross-platform, IL2CPP,
test runner. Why it exists, how it works, and - most importantly - 
what to do about tests that don't work in the portable test runner.

## Motivation

Why another test runner when there is already NUnit?

NUnit is a test running framework that uses reflection and an extensive
rich dotnet API to find and run tests. NUnit provides a broad API for asserts,
for example:

```
Assert.IsTrue(a);
Assert.That(x, Is.EqualTo(y));
```

This causes problems for DOTS-Runtime, which

1. Doesn't have reflection
2. Uses a minimal dotnet API
3. Doesn't catch execptions
4. Can't use any managed objects on job threads

To work around this, the DOTS-Runtime tests are compiled against the minimal,
reflection-less API, but run against the full dotnet stack. Burst and 
multi-threading are turned off during testing.

Meaning:

1. DOTS-Runtime is being tested in an approximated, simulated environment.
2. We can't run tests compiled to IL2CPP (which is no reflection, minimal
   API) and IL2CPP is the basis for *all* our platform support. Therefore,
   we can't test on any actual target platform.
3. Burst isn't tested.
4. Multi-threading isn't tested.

The cross-platform runner addresses all these issues.

### Value of NUnit

The full NUnit tests are still very valuable - they are shared code with
hybrid and runtime, full, rich, and reguarly catch real problems. The
cross-platform tests will likely be a subset for quite some time. There is
currently no intent to move away from the NUnit tests.

## How the Cross-Platform Runner Works

The high level point: the cross platform test runner runs in the minimal
C# and dotnet used by DOTS-Runtime, so therefore test cases must
conform to this profile.

### API Support

The cross-platform runner provides a sub-set of the NUnit Assert API. There
are two levels of support for any API:

1. The API exists and is supported (`Assert.IsTrue(x)`) and the test cases
   that use it will run (and presumably pass.)
2. The API exists and is NotSupported. (`CollectionAssert.AreEquivalent()`). 
   In this situation the code will compile, but the output will append 
   [NotSupported] before the name of the test case, and the test case will
   not be run. (See Fixing Tests, below.) This is implemented so we don't
   have to #if out a bunch of tests; they are compiled and tracked, even
   if not run.

How to tell? APIs are flagged with the [NotSupported] attribute if they will 
compile, but not be run, and output a [NotSupported] warning.

General notes:

* `Assert.IsTrue`, `Assert.AreEqual` and related methods are supported for 
   specific types, by overloads. Numbers, integers, etc. can be compared,
   but support of .Equals() on arbitrary objects in (currently) not. This
   covers the vast majority of the test cases.
* `Assert.That` is covered for the common, but very limited case 
   of: `Assert.That(x, Is.EqualTo(y))`
* `Assert.That` is it's wide general case often won't even compile.

### Code-Gen

The other part of the cross-platfom test runner is TestCaseILPP, which is
an IL post processor that scans the test assembly for [Test] tags, and
code-gens a calling function.

It supports [Test], [SetUp], and [TearDown] consistent with NUnit. (In theory,
the model is simpler than NUnit's, but I'm not aware of this having any
practical impact.) [Value] support is coming soon.

If the post processor can't handle a test, it's name will be printed
with the prefix [Limitation] and not run. The reason for the limitation
is logged as well.

## When an Assertion Fails

The test case will exit with a diagnostic message and an uncaught exception.
There is a bug open to handle this more elegantly, but you won't miss a test
failure.

## Fixing Tests (that work in NUnit)

Ways to address issues that arise, with better solutions listed first
in the category.

### Compilation Failure

1. Overload issue. For example, `Assert.Less(int, int)` is supported and
   `Assert.Less(float, float)` is not. It is very simple to add an overload
   (or possibly a generic, although that can be trickier) to handle your case,
   and everyone benefits. 
2. Wacky syntax. `Assert.That(x, Is.Not.Equal(y));` can be 
   `Assert.NotEqual(x, y)` in many cases.
3. LINQ is a problem. The DOTS-Runtime build doesn't support LINQ. It's 
   possible to add a helper library and re-implement parts of LINQ, but that
   hasn't been scoped or done. It's better to avoid LINQ in the test cases.
   If that's not practical, the #if in the next two items are a fall-back.
4. The define UNITY_PORTABLE_TEST_RUNNER is only turned on if in the runner.
   It *is* possible and useful to run the portable runner in dotnet, so
   UNITY_PORTABLE_TEST_RUNNER is not equivalent to UNITY_DOTSPLAYER_IL2CPP.
5. The define UNITY_DOTSPLAYER_IL2CPP is the "big hammer" that is turned on
   for IL2CPP builds. It can be used to exclude code from the IL2CPP cross
   platform tests, but this is obviously the least desireably option. The
   code is neither compiled, tested, or tracked. A JIRA ticket should be used
   to track this code.

### NotSupported

If the [NotSupported] tag is written out before a test name:

```
[NotSupported] 'System.Void Unity.Entities.Tests.ComponentSystemTests::UpdateDestroyedSystemThrows()' Assert.Throws
```

Then the test uses a feature that isn't supported in the Runtime. The test
is logged for tracking, but not run.

In the case above, the test is asserting for an exception; but DOTS-Runtime
doesn't support catching exceptions, so this test can't be run. Either the
test needs to be re-written to use only supported features, or will only
be run in the full dotnet NUNit runtime.
