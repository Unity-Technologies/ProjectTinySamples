using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;
using Mono.Cecil.Rocks;

namespace UnsafeUtilityPatcher
{
    class Program
    {
        class UnsafeUtilityPatcherContext
        {
            public TypeDefinition       m_PatchedClass;
            public GenericParameter     m_GenericInstanceParameter;
            public GenericInstanceType  m_GenericInstanceType;

            public UnsafeUtilityPatcherContext(TypeDefinition classType, bool isGeneric)
            {
                m_PatchedClass = classType;

                if (isGeneric)
                {
                    m_GenericInstanceParameter = m_PatchedClass.GenericParameters[0];
                    m_GenericInstanceType = m_PatchedClass.MakeGenericInstanceType(m_GenericInstanceParameter);
                }
            }
        }

        // Arguments:
        static private string   s_AssemblyPath = null;
        static private string   s_OutputPath = null;

        private static void ParseArguments(IEnumerable<string> args)
        {
            var p = new NDesk.Options.OptionSet()
                .Add("assembly=", "Assembly to process.", s => s_AssemblyPath = s)
                .Add("output=", "Output path for assembly", s => s_OutputPath = s);
            var remaining = p.Parse(args);
            if (remaining.Any())
            {
                Console.Write("Unknown option: " + remaining[0]);
                throw new NDesk.Options.OptionException();
            }
        }

        // Some constants:
        const string kUnsafeUtilityIdentifier                   = "UnsafeUtility";
        const string kUnsafeUtilityCopyPtrToStructure           = "CopyPtrToStructure";
        const string kUnsafeUtilityCopyStructureToPtr           = "CopyStructureToPtr";
        const string kUnsafeUtilityAddressOf                    = "AddressOf";
        const string kUnsafeUtilitySizeOf                       = "SizeOf";
        const string kUnsafeUtilitySizeOfNonGeneric             = "SizeOfNonGeneric";
#if UNITY_2020_1_OR_NEWER
#else
        const string kUnsafeUtilityAlignOf                      = "AlignOf";
#endif
        const string kUnsafeUtilityReadArrayElement             = "ReadArrayElement";
        const string kUnsafeUtilityWriteArrayElement            = "WriteArrayElement";
        const string kUnsafeUtilityReadArrayElementWithStride   = "ReadArrayElementWithStride";
        const string kUnsafeUtilityWriteArrayElementWithStride  = "WriteArrayElementWithStride";
        const string kUnsafeUtilityAs                           = "As";
        const string kUnsafeUtilityAsRef                        = "AsRef";
        const string kUnsafeUtilityArrayElementAsRef            = "ArrayElementAsRef";

        static void Main(string[] args)
        {
            ParseArguments(args);

            Console.WriteLine("[UnsafeUtilityPatcher] Patching assembly : " + s_AssemblyPath + " to : " + s_OutputPath);

            var assembly = AssemblyDefinition.ReadAssembly(s_AssemblyPath, new ReaderParameters { SymbolReaderProvider = CreateDebugReaderProviderFor(s_AssemblyPath) });

            var UnsafeUtilityType = assembly.MainModule.Types.Single(t => t.Name.Equals(kUnsafeUtilityIdentifier));

            var ctx = new UnsafeUtilityPatcherContext(UnsafeUtilityType, false);

            InjectUtilityCopyPtrToStructure(ctx);
            InjectUtilityCopyStructureToPtr(ctx);
            InjectUtilityAddressOf(ctx);
            InjectUtilitySizeOf(ctx);
#if UNITY_2020_1_OR_NEWER
#else
            InjectUtilityAlignOf(ctx);
#endif
            InjectUtilityReadArrayElement(ctx);
            InjectUtilityReadArrayElementWithStride(ctx);
            InjectUtilityWriteArrayElement(ctx);
            InjectUtilityWriteArrayElementWithStride(ctx);
            InjectUtilityAs(ctx);
            InjectUtilityAsRef(ctx);
            InjectUtilityArrayElementAsRef(ctx);

            EnsureTargetFolderExists(s_OutputPath);
            assembly.Write(s_OutputPath, new WriterParameters { SymbolWriterProvider = CreateDebugWriterProviderFor(s_AssemblyPath) });
        }

        private static void EnsureTargetFolderExists(string assemblyOutputPath)
        {
            var outputFolder = Path.GetDirectoryName(assemblyOutputPath);
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);
        }

        private static ISymbolWriterProvider CreateDebugWriterProviderFor(string assemblyPath)
        {
            var candidateMdbFilePath = assemblyPath + ".mdb";
            if (File.Exists(candidateMdbFilePath))
                return new MdbWriterProvider();

            var candidatePdbFilePath = Path.ChangeExtension(assemblyPath, ".pdb");
            if (File.Exists(candidatePdbFilePath))
                return new PdbWriterProvider();

            candidatePdbFilePath = assemblyPath+".pdb";
            if (File.Exists(candidatePdbFilePath))
                return new PdbWriterProvider();

            return null;
        }

        private static ISymbolReaderProvider CreateDebugReaderProviderFor(string assemblyPath)
        {
            var candidateMdbFilePath = assemblyPath + ".mdb";
            if (File.Exists(candidateMdbFilePath))
                return new MdbReaderProvider();

            var candidatePdbFilePath = Path.ChangeExtension(assemblyPath, ".pdb");
            if (File.Exists(candidatePdbFilePath))
                return new PdbReaderProvider();

            return null;
        }

        private static ILProcessor GetILProcessorForMethod(UnsafeUtilityPatcherContext ctx, string methodName, bool clear = true)
        {
            var method = ctx.m_PatchedClass.Methods.Single(m => m.Name.Equals(methodName) && m.HasGenericParameters);
            var ilProcessor = method.Body.GetILProcessor();

            if (clear)
            {
                ilProcessor.Body.Instructions.Clear();
                ilProcessor.Body.Variables.Clear();
                ilProcessor.Body.ExceptionHandlers.Clear();
            }

            return ilProcessor;
        }

        private static void LoadField(UnsafeUtilityPatcherContext ctx, string fieldName, ILProcessor ilProcessor)
        {
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_0));
            var field = ctx.m_PatchedClass.Fields.Single(f => f.Name == fieldName);
            var fieldReference = new FieldReference(field.Name, field.FieldType, ctx.m_GenericInstanceType);
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldfld, fieldReference));
        }

        private static void CallMethod(UnsafeUtilityPatcherContext ctx, string methodName, ILProcessor ilProcessor)
        {
            var method = ctx.m_PatchedClass.GetMethods().Single(m => m.Name == methodName);

            var methodReference = new MethodReference(method.Name, method.ReturnType, ctx.m_GenericInstanceType)
            {
                CallingConvention = method.CallingConvention,
                HasThis = method.HasThis,
            };

            foreach (var param in method.Parameters)
            {
                methodReference.Parameters.Add(param);
            }

            ilProcessor.Append(ilProcessor.Create(OpCodes.Call, methodReference));
        }

        private static void InjectUtilityCopyPtrToStructure(UnsafeUtilityPatcherContext ctx)
        {
            var ilProcessor = GetILProcessorForMethod(ctx, kUnsafeUtilityCopyPtrToStructure);

            /*
                ldarg.1
                ldarg.0
                ldobj !T
                stobj !T
                ret
            */

            var method = ctx.m_PatchedClass.Methods.Single(m => m.Name.Equals(kUnsafeUtilityCopyPtrToStructure));

            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_1));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_0));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldobj, method.GenericParameters[0]));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Stobj, method.GenericParameters[0]));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));
        }

        private static void InjectUtilityCopyStructureToPtr(UnsafeUtilityPatcherContext ctx)
        {
            var ilProcessor = GetILProcessorForMethod(ctx, kUnsafeUtilityCopyStructureToPtr);

            /*
                ldarg.0
                ldobj !T
                ldarg.1
                stind.i
                ret
            */

            var method = ctx.m_PatchedClass.Methods.Single(m => m.Name.Equals(kUnsafeUtilityCopyStructureToPtr));

            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_1));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_0));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldobj, method.GenericParameters[0]));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Stobj, method.GenericParameters[0]));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));
        }

        private static void InjectUtilityAddressOf(UnsafeUtilityPatcherContext ctx)
        {
            var ilProcessor = GetILProcessorForMethod(ctx, kUnsafeUtilityAddressOf);

            /*
                ldarg.0
                ret
            */

            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_0));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));
        }

        private static void InjectUtilitySizeOf(UnsafeUtilityPatcherContext ctx)
        {
            var ilProcessor = GetILProcessorForMethod(ctx, kUnsafeUtilitySizeOf);

            /*
                sizeof !T
                ret
            */

            var method = ctx.m_PatchedClass.Methods.Single(m => m.Name.Equals(kUnsafeUtilitySizeOf) && m.HasGenericParameters);

            ilProcessor.Append(ilProcessor.Create(OpCodes.Sizeof, method.GenericParameters[0]));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));
        }

        private static void InjectUtilitySizeOfNonGeneric(UnsafeUtilityPatcherContext ctx)
        {
            var ilProcessor = GetILProcessorForMethod(ctx, kUnsafeUtilitySizeOfNonGeneric);

            /*
                sizeof !T
                ret
            */

            var method = ctx.m_PatchedClass.Methods.Single(m => m.Name.Equals(kUnsafeUtilitySizeOf) && m.HasGenericParameters);

            ilProcessor.Append(ilProcessor.Create(OpCodes.Sizeof, method.GenericParameters[0]));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));
        }



#if UNITY_2020_1_OR_NEWER
#else
        private static void InjectUtilityAlignOf(UnsafeUtilityPatcherContext ctx)
        {
            var ilProcessor = GetILProcessorForMethod(ctx, kUnsafeUtilityAlignOf);

            /*
                // @todo : Implement
                ldc.i4 4
                ret
            */

            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldc_I4, 4));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));
        }
#endif

        private static void InjectUtilityAs(UnsafeUtilityPatcherContext ctx)
        {
            var ilProcessor = GetILProcessorForMethod(ctx, kUnsafeUtilityAs);

            /*
                ldarg.0
                ret
            */

            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_0));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));
        }

        private static void InjectUtilityAsRef(UnsafeUtilityPatcherContext ctx)
        {
            var ilProcessor = GetILProcessorForMethod(ctx, kUnsafeUtilityAsRef);

            /*
                ldarg.0
                ret
            */

            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_0));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));
        }

        private static void InjectUtilityArrayElementAsRef(UnsafeUtilityPatcherContext ctx)
        {
            var ilProcessor = GetILProcessorForMethod(ctx, kUnsafeUtilityArrayElementAsRef);

            /*
                ldarg.0
                ldarg.1
                sizeof !T
                mul
                add
                ret
            */

            var method = ctx.m_PatchedClass.Methods.Single(m => m.Name.Equals(kUnsafeUtilityArrayElementAsRef));

            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_0));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_1));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Conv_I8));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Sizeof, method.GenericParameters[0]));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Conv_I8));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Mul));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Conv_I));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Add));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));
        }

        private static void InjectUtilityReadArrayElement(UnsafeUtilityPatcherContext ctx)
        {
            var ilProcessor = GetILProcessorForMethod(ctx, kUnsafeUtilityReadArrayElement);

            /*
                ldarg.0
                ldarg.1
                conv.i8
                sizeof !T
                conv.i8
                mul
                conv.i
                add
                ldobj !T
                ret
            */

            var method = ctx.m_PatchedClass.Methods.Single(m => m.Name.Equals(kUnsafeUtilityReadArrayElement));

            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_0));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_1));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Conv_I8));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Sizeof, method.GenericParameters[0]));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Conv_I8));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Mul));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Conv_I));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Add));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldobj, method.GenericParameters[0]));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));
        }

        private static void InjectUtilityReadArrayElementWithStride(UnsafeUtilityPatcherContext ctx)
        {
            var ilProcessor = GetILProcessorForMethod(ctx, kUnsafeUtilityReadArrayElementWithStride);

            /*
                ldarg.0
                ldarg.1
                conv.i8
                ldarg.2
                conv.i8
                mul
                conv.i
                add
                ldobj !T
                ret
            */

            var method = ctx.m_PatchedClass.Methods.Single(m => m.Name.Equals(kUnsafeUtilityReadArrayElementWithStride));

            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_0));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_1));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Conv_I8));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_2));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Conv_I8));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Mul));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Conv_I));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Add));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldobj, method.GenericParameters[0]));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));
        }

        private static void InjectUtilityWriteArrayElement(UnsafeUtilityPatcherContext ctx)
        {
            var ilProcessor = GetILProcessorForMethod(ctx, kUnsafeUtilityWriteArrayElement);

            /*
                ldarg.0
                ldarg.1
                conv.i8
                sizeof !T
                conv.i8
                mul
                conv.i
                add
                ldarg.2
                stobj !T
                ret
            */

            var method = ctx.m_PatchedClass.Methods.Single(m => m.Name.Equals(kUnsafeUtilityWriteArrayElement));

            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_0));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_1));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Conv_I8));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Sizeof, method.GenericParameters[0]));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Conv_I8));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Mul));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Conv_I));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Add));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_2));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Stobj, method.GenericParameters[0]));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));
        }

        private static void InjectUtilityWriteArrayElementWithStride(UnsafeUtilityPatcherContext ctx)
        {
            var ilProcessor = GetILProcessorForMethod(ctx, kUnsafeUtilityWriteArrayElementWithStride);

            /*
                ldarg.0
                ldarg.1
                conv.i8
                ldarg.2
                conv.i8
                mul
                conv.i
                add
                ldarg.3
                stobj !T
                ret
            */

            var method = ctx.m_PatchedClass.Methods.Single(m => m.Name.Equals(kUnsafeUtilityWriteArrayElementWithStride));

            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_0));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_1));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Conv_I8));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_2));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Conv_I8));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Mul));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Conv_I));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Add));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_3));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Stobj, method.GenericParameters[0]));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));
        }
    }
}
