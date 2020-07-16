using System;
using System.Collections.Generic;
using System.Linq;
using Bee.Core;
using Bee.Stevedore;
using NiceIO;
using Unity.BuildTools;

class EditorToolsBuildProgram
{
    static char PathSeparator => HostPlatform.IsWindows ? ';' : ':';

    static NPath InstallationDirectory => new NPath("./../DotsEditorTools").MakeAbsolute();

    static NPath CompileEditorToolsFromSourceFileFlag { get; } = new NPath("compile-editor-tools-from-source-flag");

    static StevedoreArtifact Node
    {
        get
        {
            if (HostPlatform.IsWindows)
                return new StevedoreArtifact("node-win-x64");
            else if (HostPlatform.IsOSX)
                return StevedoreArtifact.Public("node-mac-x64");

            throw new NotImplementedException();
        }
    }
    static NPath NodeDirectory
    {
        get
        {
            if (HostPlatform.IsWindows)
                return Node.Path.MakeAbsolute();
            else if (HostPlatform.IsOSX)
                return Node.Path.Combine("bin").MakeAbsolute();

            throw new NotImplementedException();
        }
    }

    public static void Setup(NPath rootPath)
    {
        Backend.Current.RegisterFileInfluencingGraph(CompileEditorToolsFromSourceFileFlag);

        SetupCompileEditorTools(rootPath);
        SetupGetEditorToolsFromStevedore();
    }

    static void SetupCompileEditorTools(NPath rootPath)
    {
        // since this target and `get-editor-tools` target outputs the same files
        // we cannot have these two targets side by side in the dag.
        // We need this to generate the only correct target 
        if (!CompileEditorToolsFromSourceFileFlag.FileExists())
            return;

        var editorToolsSourceDirectory = rootPath.Combine("EditorTools/Src");
        Backend.Current.Register(Node);
        var env = new Dictionary<string, string>()
        {
            { "PATH", $"{NodeDirectory.ToString()}{PathSeparator}{Environment.GetEnvironmentVariable("PATH")}" }
        };
        var dependencies = new List<NPath>();

        // Iterate all folders in Tools and process them
        foreach (var toolDir in editorToolsSourceDirectory.Contents())
        {
            if (toolDir.FileExists("package.json"))
            {
                var packageLockJsonFilePath = toolDir.Combine("package-lock.json");
                var packageJsonFilePath = toolDir.Combine("package.json");

                // Run npm install
                Backend.Current.AddAction($"npm install",
                    targetFiles: new[] { packageLockJsonFilePath },
                    inputs: new[] { Node.Path, packageJsonFilePath },
                    executableStringFor: $"cd {toolDir.InQuotes()} && npm install",
                    commandLineArguments: Array.Empty<string>(),
                    environmentVariables: env,
                    allowUnwrittenOutputFiles: true);

                dependencies.Add(packageLockJsonFilePath);

                // Run package
                var inputs = new List<NPath>
                {
                    Node.Path,
                    packageLockJsonFilePath
                };

                var indexJsNotInModules = toolDir.Files("index.js", true).Where(p => !p.IsChildOf(toolDir.Combine("node_modules")));
                inputs.AddRange(indexJsNotInModules);
                var toolInstallDir = InstallationDirectory.Combine(toolDir.FileName);

                Backend.Current.AddAction($"package",
                    targetFiles: new[] { toolInstallDir.Combine($"DotsEditorTools-win.exe"), toolInstallDir.Combine($"DotsEditorTools-macos") },
                    inputs: inputs.ToArray(),
                    executableStringFor: $"cd {toolDir.InQuotes()} && npm run package -- --out-path {toolInstallDir.InQuotes()} --targets win-x64,macos-x64 .",
                    commandLineArguments: Array.Empty<string>(),
                    environmentVariables: env,
                    allowUnwrittenOutputFiles: true);

                dependencies.Add(toolInstallDir.Combine($"DotsEditorTools-win.exe"));
                dependencies.Add(toolInstallDir.Combine($"DotsEditorTools-macos"));
            }
            else // Not a node tool, just copy files recursively
            {
                foreach (var file in toolDir.Files(true))
                {
                    if (file.FileName == "extrabeetmpfile")
                        continue;

                    var target = file.ToString().Replace(editorToolsSourceDirectory.ToString(), InstallationDirectory.ToString());
                    CopyTool.Instance().Setup(target, file);
                    dependencies.Add(target);
                }
            }
        }

        Backend.Current.AddAliasDependency("compile-editor-tools", dependencies.ToArray());
    }

    static void SetupGetEditorToolsFromStevedore()
    {
        // since this target and `get-editor-tools` target outputs the same files
        // we cannot have these two targets side by side in the dag.
        // We need this to generate the only correct target 
        if (CompileEditorToolsFromSourceFileFlag.FileExists())
            return;

        var executablesFromEditorTools = new HashSet<string>
        {
            "artifacts/Stevedore/dots-editor-tools/images/osx/cwebp",
            "artifacts/Stevedore/dots-editor-tools/images/osx/moz-cjpeg",
            "artifacts/Stevedore/dots-editor-tools/images/osx/pngcrush",
            "artifacts/Stevedore/dots-editor-tools/manager/DotsEditorTools-macos",
        };

        var EditorTools = new StevedoreArtifact("dots-editor-tools");
        Backend.Current.Register(EditorTools);

        var dependencies = new List<NPath>();
        foreach (var file in EditorTools.GetFileList())
        {
            var target = new NPath(file.ToString().Replace(EditorTools.Path.ToString(), InstallationDirectory.ToString()));

            if ((HostPlatform.IsOSX || HostPlatform.IsLinux) && executablesFromEditorTools.Contains(file.ToString()))
                Backend.Current.AddAction("copy and chmod +x", new[] { target }, new[] { file }, $"cp {file.InQuotes()} {target.InQuotes()} && chmod +x {target.InQuotes()}", Array.Empty<string>());
            else
                CopyTool.Instance().Setup(target, file);

            dependencies.Add(target);
        }

        Backend.Current.AddAliasDependency("get-editor-tools", dependencies.ToArray());
    }

    
}
