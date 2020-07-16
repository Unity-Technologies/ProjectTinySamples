using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Unity.Build;
using Unity.Platforms;

namespace Unity.Entities.Runtime.Build
{
    internal static class BeeTools
    {
        static readonly Regex BeeProgressRegex = new Regex(@"^\[\s*(?'nominator'\d+)\/(?'denominator'\d+).*] (?'annotation'.*)$", RegexOptions.Compiled);
        static readonly Regex BeeBusyRegex = new Regex(@"^\[\s*BUSY.*\] (?'annotation'.*)$", RegexOptions.Compiled);

        struct BeeProgressInfo
        {
            public float Progress;
            public string Info;
            public string FullInfo;
            public bool IsDone;
            public int ExitCode;
            public Process Process;
        }

        static IEnumerator<BeeProgressInfo> Run(string arguments, StringBuilder command, StringBuilder output, DirectoryInfo workingDirectory = null)
        {
            var beeExe = Path.GetFullPath($"{Constants.DotsRuntimePackagePath}/bee~/bee.exe");
            var executable = beeExe;
            arguments = "--no-colors " + arguments;

#if !UNITY_EDITOR_WIN
            arguments = "\"" + executable + "\" " + arguments;
            executable = Path.Combine(UnityEditor.EditorApplication.applicationContentsPath,
                "MonoBleedingEdge/bin/mono");
#endif

            command.Append(executable);
            command.Append(" ");
            command.Append(arguments);

            var progressInfo = new BeeProgressInfo()
            {
                Progress = 0.0f,
                Info = null
            };

            void ProgressHandler(object sender, DataReceivedEventArgs args)
            {
                if (string.IsNullOrWhiteSpace(args.Data))
                {
                    return;
                }

                var match = BeeProgressRegex.Match(args.Data);
                if (match.Success)
                {
                    var num = match.Groups["nominator"].Value;
                    var den = match.Groups["denominator"].Value;
                    if (int.TryParse(num, out var numInt) && int.TryParse(den, out var denInt))
                    {
                        progressInfo.Progress = (float)numInt / denInt;
                    }
                    progressInfo.Info = ShortenAnnotation(match.Groups["annotation"].Value);
                }

                var busyMatch = BeeBusyRegex.Match(args.Data);
                if (busyMatch.Success)
                {
                    progressInfo.Info = ShortenAnnotation(busyMatch.Groups["annotation"].Value);
                }

                progressInfo.FullInfo = args.Data;
                lock (output)
                {
                    output.AppendLine(args.Data);
                }
            }

            var config = new ShellProcessArgs()
            {
                Executable = executable,
                Arguments = new[] { arguments },
                WorkingDirectory = workingDirectory,
#if !UNITY_EDITOR_WIN
                // bee requires external programs to perform build actions
                EnvironmentVariables = new Dictionary<string, string>()
                {
                    {"PATH", string.Join(":",
                        Path.Combine(UnityEditor.EditorApplication.applicationContentsPath,
                            "MonoBleedingEdge/bin"),
                        "/bin",
                        "/usr/bin",
                        "/usr/local/bin")}
                },
#else
                EnvironmentVariables = null,
#endif
                OutputDataReceived = ProgressHandler,
                ErrorDataReceived = ProgressHandler
            };

            var bee = Shell.RunAsync(config);
            progressInfo.Process = bee;

            yield return progressInfo;

            const int maxBuildTimeInMs = 30 * 60 * 1000; // 30 minutes

            var statusEnum = Shell.WaitForProcess(bee, maxBuildTimeInMs, config.MaxIdleKillIsAnError);
            while (statusEnum.MoveNext())
            {
                yield return progressInfo;
            }

            progressInfo.Progress = 1.0f;
            progressInfo.IsDone = true;
            progressInfo.ExitCode = bee.ExitCode;
            progressInfo.Info = "Build completed";
            yield return progressInfo;
        }

        static string ShortenAnnotation(string annotation)
        {
            var split = annotation.Split(' ');
            for (int i = 0; i != split.Length; i++)
            {
                int lastSlash = split[i].LastIndexOf('/');
                if (lastSlash != -1)
                {
                    split[i] = split[i].Substring(lastSlash + 1);
                }
            }
            annotation = string.Join(" ", split);
            return annotation;
        }

        public class BeeRunResult
        {
            public int ExitCode { get; }
            public bool Succeeded => ExitCode == 0;
            public bool Failed => !Succeeded;
            public string Command { get; }
            public string Output { get; }
            public string Error => Failed? Output.TrimStart("##### Output").TrimStart('\n', '\r') : string.Empty;

            public BeeRunResult(int exitCode, string command, string output)
            {
                ExitCode = exitCode;
                Command = command;
                Output = output;
            }
        }

        public static BeeRunResult Run(string arguments, DirectoryInfo workingDirectory, BuildProgress progress = null)
        {
            var command = new StringBuilder();
            var output = new StringBuilder();

            var beeProgressInfo = Run(arguments, command, output, workingDirectory);
            while (beeProgressInfo.MoveNext())
            {
                if (progress?.Update(beeProgressInfo.Current.Info, beeProgressInfo.Current.Progress) ?? false)
                {
                    beeProgressInfo.Current.Process?.Kill();
                    return new BeeRunResult(-1, command.ToString(), "Build was cancelled.");
                }
            }

            return new BeeRunResult(beeProgressInfo.Current.ExitCode, command.ToString(), output.ToString());
        }

        static string TrimStart(this string str, string value)
        {
            var index = str.IndexOf(value);
            return index >= 0 ? str.Substring(index + value.Length) : str;
        }
    }
}
