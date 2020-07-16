# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.26.0] - 2020-05-15
* Fixes the `OutputBuildDirectory` BuildComponent to accept absolute paths. Previously builds would fail whenever an absolute path was used.
* Removed expired API `OptionSet.GetOptionForName(string option)`
* Updated minimum Unity Editor version to 2019.3.12f1 (84b23722532d)

## [0.25.0] - 2020-04-30
* Added an error message guiding the user to fix their BuildConfiguration asset if the asset name contains a space, rather than having the build fail with a seemingly unrelated error.
* Fixed an issue where the first component defined in an assembly would not get a generated Equals() and GetHashCode() implementation which could result in incorrect runtime behaviour as components could not be properly compared.

## [0.24.0] - 2020-04-09
* Added Burst support for Android.
* Added Player Connection support allowing a DOTS Runtime application to interact with Unity Editor.
* Added Profiler support allowing DOTS Runtime to interact with Unity Editor profiler and integrate profiling into builds.
* Removed support for IJobForEach from DOTS-Runtime.
* Added `Generate DOTS C# Solution` option to the editor's Assets toolbar. This window allows DOTS Runtime users to select which buildconfigurations to include their DOTS Runtime solution explicitly, rather than the solution only containing whatever was built recently.
* The `DotsRuntimeBuildProfile.RootAssembly` field has been moved into a new `IBuildComponent` called `DotsRuntimeRootAssembly` which is required for building BuildConfiguration assets using the "Default DOTS Runtime Pipeline". This separation is made to allow build configurations to more easily share settings across multiple Root Assemblies.
* GUID references are now supported in asmdefs.
* The `DotsRuntimeBuildProfile.UseBurst` setting has been moved to the `DotsRuntimeBurstSettings` build component.
* The `DotsRuntimeScriptingDefines` component has been replaced by the `DotsRuntimeScriptingSettings` build component. This replacement is made to allow for scripting settings to all live in a common location.
* Fixes an issue where old build artifacts could cause builds to break after updating `com.unity.tiny` if build configuration processing code has been changed.

## [0.23.0] - 2020-03-03
* Added window to select what configs to generate the dots solution with.
* Stopped requiring a reference to Unity.Tiny.Main and Unity.Tiny.EntryPoint for a root assembly to work. Implicitly adds those if an entry point is not referenced.

## [0.22.0] - 2020-02-05
* Add new required Dots runtime project settings build component to author the DisplayInfo runtime component (resolution, color space ..)


## [0.2.0] - 2020-01-21

* Full codegen support for Jobs API

## [0.1.0] - 2020-12-14

* Fixed "Project 'buildprogram' has already been added to this solution" error visible when opening generated solutions in VisualStudio.
* Update `Bee.exe` with a signed version
* Update the package to use Unity '2019.3.0f1' or later
* Initial release of *Dots Runtime*.
