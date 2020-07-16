using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Security.Policy;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using SecurityAction = Mono.Cecil.SecurityAction;

public struct KnuthHash
{
    public static KnuthHash Create()
    {
        return new KnuthHash() {Value = 3074457345618258791ul};
    }

    public static string HashString(string s)
    {
        var knuthHash = KnuthHash.Create();
        knuthHash.Add(s);
        return knuthHash.Value.ToString();
    }

    public ulong Value { get; private set; }

    public void Add(int value)
    {
        Value += (ulong)value;
        Value *= 3074457345618258799ul;
    }

    public void Add(ulong value)
    {
        Value += value;
        Value *= 3074457345618258799ul;
    }

    public void Add(string read)
    {
        foreach (char t in read)
        {
            Value += t;
            Value *= 3074457345618258799ul;
        }

        Add(0);
    }
        

    public void Add(IEnumerable<string> many)
    {
        foreach (var one in many)
            Add(one);
    }

    public void Add(bool value)
    {
        Add(value ? 1 : 0);
    }
}

namespace BurstPatcher
{
    internal class Program
    {
        public static List<string> burstargs { get; } = new List<string>();
        public static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("usage: BurstPatcher.exe <input assembly path> <output assembly path> <response file to write for burst>");
                return 1;
            }

            var resolver = new DefaultAssemblyResolver();
            var p = System.IO.Path.GetDirectoryName(args[0]);
            
            /*
             * the assemblies provided to the patcher are assumed to have their dependencies next to them
             */
            resolver.AddSearchDirectory(p);
            var pdbPath = Path.ChangeExtension(args[0], "pdb");
            bool readSymbols = File.Exists(pdbPath);
            ModuleDefinition module = ModuleDefinition.ReadModule(args[0], new ReaderParameters {ReadSymbols = readSymbols, AssemblyResolver = resolver});
            foreach (TypeDefinition type in module.Types)
            {
                VisitMethods(type);
            }

            module.Write(args[1], new WriterParameters {WriteSymbols = readSymbols});
            module.Dispose();

            if (burstargs.Count == 0)
            {
                FileStream myFileStream = File.Open(args[2], FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                myFileStream.Close();
                myFileStream.Dispose();
                File.SetLastWriteTimeUtc(args[2], DateTime.UtcNow);
                return 0;
            }
            
            System.IO.File.WriteAllLines(args[2], burstargs);
            return 0;
        }

        private static void VisitMethods(TypeDefinition type)
        {
            foreach (var type2 in type.NestedTypes)
                VisitMethods(type2);

            foreach (var method in type.Methods)
            {
                if (!method.HasCustomAttributes ||
                        !method.CustomAttributes.Any(a => a.AttributeType.ToString().Contains("BurstCompile")) ||
                        method.IsConstructor) 
                        continue;

                var hash = new KnuthHash();
                var sb = new StringBuilder();
                sb.Append("--method=");
                var methodDeclaringType = method.DeclaringType;
                hash.Add(methodDeclaringType.FullName);
                sb.Append($"{methodDeclaringType.FullName.Replace('/', '+')}, {methodDeclaringType.Module.Assembly}");
                sb.Append("::");
                sb.Append(method.Name);
                hash.Add(method.Name);
                sb.Append("(");

                
                var ps = method.Parameters.ToArray();
                if (ps.Length > 0)
                {
                    for (int i = 0; i < ps.Length - 1; i++)
                    {
                        var p = ps[i];
                        var paramString = $"{p.ParameterType.FullName}, {p.ParameterType.Scope}|";
                        sb.Append(paramString);
                        hash.Add(paramString);
                    }

                    var lastParamString =
                        $"{ps[ps.Length - 1].ParameterType.FullName}, {ps[ps.Length - 1].ParameterType.Scope}";
                    hash.Add(lastParamString);
                    sb.Append(lastParamString);
                }

                sb.Append(")");
                sb.Append("--");
                // if you don't add a letter then cpp can't parse it if it's all decimals
                var hashvalue = "burstedmethod_" + hash.Value;
                sb.Append(hashvalue);
                burstargs.Add(sb.ToString());
                ModuleReference internalModuleRef = null;

                var mainModuleModuleReferences = type.Module.Assembly.MainModule.ModuleReferences;

                if (mainModuleModuleReferences.Any(m => m.Name == "__Internal"))
                    internalModuleRef = type.Module;
                else
                {
                    internalModuleRef = new ModuleReference("__Internal");
                    mainModuleModuleReferences.Add(internalModuleRef);
                }
                
                method.Body = null;
                method.Attributes |= MethodAttributes.PInvokeImpl;
                method.PInvokeInfo = new PInvokeInfo( 
                    PInvokeAttributes.CallConvCdecl,
                    hashvalue,
                    internalModuleRef);
            }
        }
    }
}
