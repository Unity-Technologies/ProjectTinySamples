using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CommandLine;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

public class NamedILPostProcessorWrapper
{
    public string Name { get; }
    public ILPostProcessor ILPostProcessor { get; }

    public NamedILPostProcessorWrapper(string name, ILPostProcessor ilpostProcessor)
    {
        Name = name;
        ILPostProcessor = ilpostProcessor;
    }
}

public class ILPostProcessorRunner
{
    static NamedILPostProcessorWrapper[] NamedILPostProcessors;
    static CompiledAssembly InputAssembly;
    static string OutputAsmPath;
    static string OutputPdbPath;

    /// <summary>
    /// Commandline Options
    /// </summary>
    class Options
    {
        [Option('o', "outputDir", Required = true, HelpText = "Set the directory for processed assemblies to be written to.")]
        public string OutputDir { get; set; }

        [Option('p', "processors", Separator = ',', Required = false, HelpText = "Comma delimited list of ILPostProcessor Assembly paths.")]
        public IEnumerable<string> ILPostProcessorPaths { get; set; }

        [Option('r', "assemblyReferences", Separator = ',', Required = false, HelpText = "Comma delimited paths to all reference assemblies of the assembly specified for the --assembly argument.")]
        public IEnumerable<string> ReferenceAssemblyPaths { get; set; }

        [Option('d', "scriptingDefines", Separator = ',', Required = false, HelpText = "Comma delimited list of scripting defines to be passed to the ILPostProcessors.")]
        public IEnumerable<string> ScriptingDefines { get; set; }
    }

    /// <summary>
    /// In-memory representation of a built assembly, with full path strings to the assemblies it references
    /// </summary>
    public class CompiledAssembly : ICompiledAssembly
    {
        public string Name { get; }
        public string[] References { get; }
        public InMemoryAssembly InMemoryAssembly { get; set; }
        public string[] Defines { get; }

        public CompiledAssembly(string asmPath, string[] referencePaths, string[] defines)
        {
            var peData = File.ReadAllBytes(asmPath);

            byte[] pdbData = null;
            var pdbPath = Path.ChangeExtension(asmPath, "pdb");
            if (File.Exists(pdbPath))
                pdbData = File.ReadAllBytes(pdbPath);

            Name = Path.GetFileNameWithoutExtension(asmPath);
            References = referencePaths;
            InMemoryAssembly = new InMemoryAssembly(peData, pdbData);
            Defines = defines;
        }

        public void Save()
        {
            File.WriteAllBytes(OutputAsmPath, InMemoryAssembly.PeData);

            if (InMemoryAssembly.PdbData != null)
                File.WriteAllBytes(OutputPdbPath, InMemoryAssembly.PdbData);
        }
    }

    static bool ProcessArgs(string[] args)
    {
        bool success = true;

        try
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(o =>
                {
                    NamedILPostProcessors = o.ILPostProcessorPaths
                        .Select(p => Assembly.LoadFrom(p))
                        .SelectMany(asm => asm.GetTypes().Where(t => typeof(ILPostProcessor).IsAssignableFrom(t)))
                        .Select(t => new NamedILPostProcessorWrapper(t.FullName, (ILPostProcessor)Activator.CreateInstance(t)))
                        .ToArray();

                    var assemblyPath = args[0];
                    OutputAsmPath = Path.Combine(o.OutputDir, Path.GetFileName(assemblyPath));
                    OutputPdbPath = Path.ChangeExtension(OutputAsmPath, "pdb");
                    InputAssembly = new CompiledAssembly(assemblyPath, o.ReferenceAssemblyPaths.ToArray(), o.ScriptingDefines.ToArray());
                });
        }
        catch (Exception e)
        {
            Console.WriteLine("ILPostProcessorRunner caught the following exception while processing arguments:\n" + e);

            var rtle = e as ReflectionTypeLoadException;
            if (rtle != null)
            {
                foreach (Exception inner in rtle.LoaderExceptions)
                {
                    Console.WriteLine(inner);
                }
            }

            success = false;
        }

        return success;
    }

    public static NamedILPostProcessorWrapper[] SortILPostProcessors()
    {
        // Sort by name to ensure we have some determinism on how we are processing assemblies should
        // two ILPostProcessors potentially conflict. We will need to likely add a more structured ordering later
        var sortedList = new List<NamedILPostProcessorWrapper>(NamedILPostProcessors);
        sortedList.Sort((a, b) => a.Name.CompareTo(b.Name));

        return sortedList.ToArray();
    }

    public static void RunILPostProcessors(NamedILPostProcessorWrapper[] ilpostProcessors)
    {
        foreach (var namedProcessor in ilpostProcessors)
        {
            var processor = namedProcessor.ILPostProcessor;
            if (!processor.WillProcess(InputAssembly))
                continue;

            using (new Marker($"\t{namedProcessor.Name}"))
            {
                var result = processor.Process(InputAssembly);
                if (result == null)
                    continue;

                if (result.InMemoryAssembly != null)
                {
                    InputAssembly.InMemoryAssembly = result.InMemoryAssembly;
                }

                if (result.Diagnostics != null)
                {
                    HandleDiagnosticMessages(processor.GetType().Name, InputAssembly.Name, result.Diagnostics);
                }
            }
        }
    }

    public static void HandleDiagnosticMessages(string ilppName, string asmName, List<DiagnosticMessage> messageList)
    {
        bool hasError = false;

        StringBuilder sb = new StringBuilder();
        foreach (var diagMsg in messageList)
        {
            if (diagMsg.DiagnosticType == DiagnosticType.Error)
            {
                hasError = true;
            }

            if (!string.IsNullOrEmpty(diagMsg.File))
                sb.Append($"{diagMsg.File}({diagMsg.Line},{diagMsg.Column}): ");

            sb.Append($"{diagMsg.MessageData}\n");
        } 

        Console.WriteLine(sb.ToString());

        if (hasError)
        {
            throw new Exception($"ILPostProcessorRunner '{ilppName}' had a fatal error processing '{asmName}'\n" +
                $"Refer to diagnostic messages above for more details. \n" +
                $"Exiting..."
            );
        }
    }

    public static int Main(string[] args)
    {
        using (new Marker("ILPostProcessorRunner Time"))
        {

            if (!ProcessArgs(args))
                return -1;

            var sortedILPostProcessors = SortILPostProcessors();
            RunILPostProcessors(sortedILPostProcessors);

            // Write out our Input Assembly, processed or not (in which case it's just a copy)
            InputAssembly.Save();
        }
        return 0;
    }


    struct Marker : IDisposable
    {
        System.Diagnostics.Stopwatch Stopwatch;
        string Name;
        public Marker(string name)
        {
            Name = name;
            Stopwatch = System.Diagnostics.Stopwatch.StartNew();
        }

        public void Dispose()
        {
            Stopwatch.Stop();
            Console.WriteLine($"{Name}: {Stopwatch.ElapsedMilliseconds}ms");
        }
    }
}
