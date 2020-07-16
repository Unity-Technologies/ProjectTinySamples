using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.Entities.BuildUtils;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;
using System.Runtime.CompilerServices;
using Mono.Cecil.Pdb;
using Newtonsoft.Json;

namespace Unity.ZeroPlayer
{
    public class Autoclose : IDisposable
    {
        Action disposeClose;
        private bool called = false;

        public Autoclose(Action disposeClose)
        {
            this.disposeClose = disposeClose;
        }

        public void Dispose()
        {
            if (!called)
            {
                called = true;
                disposeClose();
            }
        }
    }

    internal static class Extensions
    {
        // Extension function to Mono.Cecil to allow for creating a reference to a method for a type with generic parameters such as NativeArray<T>
        // https://groups.google.com/forum/#!topic/mono-cecil/mCat5UuR47I
        public static MethodReference MakeHostInstanceGeneric(this MethodReference self, params TypeReference[] args)
        {
            GenericInstanceType generic = self.DeclaringType.MakeGenericInstanceType(args);
            var reference = new MethodReference(self.Name, self.ReturnType, generic)
            {
                HasThis = self.HasThis,
                ExplicitThis = self.ExplicitThis,
                CallingConvention = self.CallingConvention
            };

            foreach (var parameter in self.Parameters)
            {
                reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));
            }

            foreach (var genericParam in self.GenericParameters)
            {
                reference.GenericParameters.Add(new GenericParameter(genericParam.Name, reference));
            }

            return reference;
        }

        public static void WriteNameValue<T>(this JsonTextWriter jw, in string name, in T value)
        {
            jw.WritePropertyName(name);
            jw.WriteValue(value);
        }

        public static Autoclose WriteStartObjectAuto(this JsonTextWriter jw)
        {
            jw.WriteStartObject();
            return new Autoclose(() => jw.WriteEndObject());
        }

        public static Autoclose WriteStartArrayAuto(this JsonTextWriter jw)
        {
            jw.WriteStartArray();
            return new Autoclose(() => jw.WriteEndArray());
        }

        public static Autoclose WriteStartConstructorAuto(this JsonTextWriter jw, in string name)
        {
            jw.WriteStartConstructor(name);
            return new Autoclose(() => jw.WriteEndConstructor());
        }
    }

    public class TypeRegGen
    {
        internal class AssemblyResolver : DefaultAssemblyResolver
        {
            public Dictionary<string, int> AssemblyNameToIndexMap;
            public List<AssemblyDefinition> AssemblyDefinitions;

            public AssemblyResolver(ref List<AssemblyDefinition> assemblyDefinitions)
            {
                AssemblyNameToIndexMap = new Dictionary<string, int>();
                AssemblyDefinitions = assemblyDefinitions;
            }

            public void Add(AssemblyDefinition knownAssembly)
            {
                int index = AssemblyDefinitions.Count;
                AssemblyDefinitions.Add(knownAssembly);
                AssemblyNameToIndexMap.Add(knownAssembly.Name.FullName, index);
            }

            public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                if (AssemblyNameToIndexMap.TryGetValue(name.FullName, out var asmIndex))
                {
                    return AssemblyDefinitions[asmIndex];
                }

                return base.Resolve(name, parameters);
            }

            protected override void Dispose(bool disposing)
            {
                foreach (var asm in AssemblyDefinitions)
                    asm.Dispose();

                AssemblyDefinitions.Clear();
                base.Dispose(disposing);
            }
        }

        public static void Main(string[] args)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            TypeRegGen typeRegGen = new TypeRegGen();
            typeRegGen.GenerateTypeRegistry(args);

            stopwatch.Stop();
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            Console.WriteLine("Static Type Registry Generation Time: {0}ms", elapsedMs);
        }

        public TypeRegGen()
        {
            m_ArchBits = 64;
            m_AssemblyDefs = new List<AssemblyDefinition>();
        }

        public void GenerateTypeRegistry(string[] args)
        {
            var assemblyResolver = new AssemblyResolver(ref m_AssemblyDefs);
            var symbolReaderProvider = new DefaultSymbolReaderProvider();

            ProcessArgs(args, assemblyResolver, symbolReaderProvider);

            // This call can be removed once IL2CPP supports module initializers
            InjectRegisterAllAssemblyRegistries();

            m_interfaceGen = new InterfaceGen(m_AssemblyDefs, m_BurstEnabled);
            m_interfaceGen.AddMethods();
            m_interfaceGen.PatchJobsCode();
            m_interfaceGen.InjectBurstInfrastructureMethods();

            ScanAndInjectCustomBootstrap();

            WriteModifiedAssemblies(in m_interfaceGen.TypesToMakePublic);
            //SaveDebugMeta(typeGenInfoList, m_Systems, m_interfaceGen.JobDescList);

            assemblyResolver.Dispose();
            m_MscorlibAssembly.Dispose();
            m_EntityAssembly.Dispose();
        }

        void InjectRegisterAllAssemblyRegistries()
        {
            var typeManager = m_EntityAssembly.MainModule.GetType("Unity.Entities.TypeManager");
            var registerAssemblyType = m_EntityAssembly.MainModule.GetType("Unity.Entities.TypeRegistry");
            var registerFn = typeManager.GetMethods().First(m => m.Name == "RegisterStaticAssemblyTypes");
            var registerAsmFn = typeManager.GetMethods().First(m => m.Name == "RegisterAssemblyTypes");
            var il = registerFn.Body.GetILProcessor();
            registerFn.Body.Instructions.Clear();

            var asmRegTypeList = new List<TypeDefinition>();
            foreach (var asm in m_AssemblyDefs)
            {
                if (asm.MainModule.AssemblyReferences.FirstOrDefault(r => r.Name == "Unity.Entities") == null)
                    continue;

                foreach (var type in asm.MainModule.GetAllTypes().Where(t => t.IsClass))
                {
                    if (type.FullName == "Unity.Entities.CodeGeneratedRegistry.AssemblyTypeRegistry")
                    {
                        asmRegTypeList.Add(type);
                    }
                }
            }

            PushNewArray(ref il, registerAssemblyType, asmRegTypeList.Count);
            for (int i = 0; i < asmRegTypeList.Count; ++i)
            {
                var type = asmRegTypeList[i];
                var field = m_EntityAssembly.MainModule.ImportReference(type.Fields.First(f => f.Name == "TypeRegistry"));
                PushNewArrayElement(ref il, i);
                il.Emit(OpCodes.Ldsfld, field);
                il.Emit(OpCodes.Stelem_Ref);
            }

            il.Emit(OpCodes.Call, registerAsmFn);
            il.Emit(OpCodes.Ret);
        }

        public void ScanAndInjectCustomBootstrap()
        {
            const string TinyCoreAssemblyName = "Unity.Tiny.Core";
            const string CustomBootstrapName = "Unity.Entities.ICustomBootstrap";
            var tinyCoreAsmDef = m_AssemblyDefs.FirstOrDefault(a => a.Name.Name == TinyCoreAssemblyName);
            if (tinyCoreAsmDef == null)
                throw new ArgumentException($"Failed to find {TinyCoreAssemblyName} assembly for modifying.");

            var customBootstraps = new List<TypeDefinition>();
            foreach (AssemblyDefinition asm in m_AssemblyDefs)
            {
                if (!asm.MainModule.AssemblyReferences.Any(anr => anr.Name == TinyCoreAssemblyName))
                    continue;

                foreach (TypeDefinition t in asm.MainModule.GetAllTypes())
                {
                    if (!t.IsPrimitive && DoesTypeInheritInterface(t, CustomBootstrapName))
                    {
                        customBootstraps.Add(t);
                    }
                }
            }

            var defaultWorldInitDef = tinyCoreAsmDef.MainModule.GetType("Unity.Entities.DefaultWorldInitialization");
            var getBootstrapFn = defaultWorldInitDef.Methods.First(m => m.Name == "GetCustomBootstrap");
            getBootstrapFn.Body.Instructions.Clear();
            var il = getBootstrapFn.Body.GetILProcessor();

            if (customBootstraps.Count == 0)
                il.Emit(OpCodes.Ldnull);
            else
            {
                TypeDefinition bootstrap = customBootstraps[0];
                // Hybrid has the odd logic of that if there is more than one custombootstrap, choose the one that
                // is most specialized. (e.g Type A, or Type B : A, choose B since it further specializes/extends A)
                // Of course this totally breaks down if you have A, B : A, C : A but we'll just choose the first
                // one we find since that is what Hybrid does as well until we can change how Hybrid works.
                if (customBootstraps.Count > 1)
                {
                    int bootstrapIndex = 0;
                    int maxDepth = 0;
                    for (int i = 0; i < customBootstraps.Count; ++i)
                    {
                        // Note this isn't 100% correct since we aren't checking the depth of the specialization for
                        // types underneath the type implementing ICustomBootstrap. This detail is ignored for now as
                        // it's uncommon we will have more than one ICustomBootstrap anyway.
                        var t = customBootstraps[i];
                        int depth = 1;
                        while (t.BaseType != null)
                        {
                            t = t.BaseType.Resolve();
                            depth++;
                        }

                        if (depth > maxDepth)
                            bootstrapIndex = i;
                    }

                    bootstrap = customBootstraps[bootstrapIndex];
                }

                ForceTypeAsPublic(bootstrap);
                var bootstrapCtor = tinyCoreAsmDef.MainModule.ImportReference(bootstrap.GetConstructors().First(c => !c.HasParameters));
                il.Emit(OpCodes.Newobj, bootstrapCtor);
            }
            il.Emit(OpCodes.Ret);
        }

        internal static bool DoesTypeInheritInterface(TypeDefinition typeDef, string interfaceName)
        {
            if (typeDef == null)
                return false;

            return (typeDef.Interfaces.Any(i => i.InterfaceType.FullName.Equals(interfaceName)) || (typeDef.BaseType != null && DoesTypeInheritInterface(typeDef.BaseType.Resolve(), interfaceName)));
        }

        internal void ProcessArgs(string[] args, AssemblyResolver assemblyResolver, ISymbolReaderProvider symbolReaderProvider)
        {
            int argIndex = 0;
            m_OutputDir = Path.GetFullPath(args[argIndex++]);

            var archBitsStr = args[argIndex++];
            var profileStr = args[argIndex++];
            var burstStr = args[argIndex++];
            var configStr = args[argIndex++].ToLower();

            if (!int.TryParse(archBitsStr, out m_ArchBits) || (m_ArchBits != 32 && m_ArchBits != 64))
                throw new ArgumentException($"Invalid architecture-bits passed in as second argument. Received '{archBitsStr}', Expected '32' or '64'.");

            if (burstStr == "Bursted")
                m_BurstEnabled = true;

            for (int i = argIndex; i < args.Length; ++i)
            {
                string normalizedPath = Path.GetFullPath(args[i]);
                if (!File.Exists(normalizedPath))
                {
                    Console.WriteLine($"Could not find assembly '{normalizedPath}': Please check your commandline arguments.");
                    continue;
                }

                // If we know we are reading from where we will be writing, ensure we open for readwrite
                bool bReadWrite = Path.GetDirectoryName(normalizedPath) == m_OutputDir;
                // This assembly tends to be in escalated privledge directories so we only open for read (we shouldn't need to write to it anyway)
                if (Path.GetFileNameWithoutExtension(normalizedPath) == "UnityEngine.CoreModule")
                {
                    bReadWrite = false;
                }

                var pdbPath = Path.ChangeExtension(normalizedPath, "pdb");
                var readerParams = new ReaderParameters() { AssemblyResolver = assemblyResolver, ReadWrite = bReadWrite, InMemory = true };
                if (File.Exists(pdbPath))
                {
                    readerParams.SymbolReaderProvider = symbolReaderProvider;
                    readerParams.ReadSymbols = true;
                }

                var asm = AssemblyDefinition.ReadAssembly(normalizedPath, readerParams);
                assemblyResolver.Add(asm);
            }

            // Entity is special so we maintain a specific reference to it so we can ensure it is always registed as typeIndex 1 (0 being reserved for null)
            m_EntityAssembly = m_AssemblyDefs.First(asm => asm.Name.Name == "Unity.Entities");

            m_MscorlibAssembly = m_AssemblyDefs.FirstOrDefault(asm => asm.Name.Name == "mscorlib");
            if (m_MscorlibAssembly == null)
            {
                var readerParams = new ReaderParameters() { AssemblyResolver = assemblyResolver };
                m_MscorlibAssembly = AssemblyDefinition.ReadAssembly(typeof(object).Assembly.Location, readerParams);
                assemblyResolver.Add(m_MscorlibAssembly);
                m_WriteOutMscorlib = false; // This was not an input, therefore we should not write it out
            }
        }

        private static void ForceTypeAsPublicRecurse(TypeDefinition typeDef)
        {
            if (typeDef == null)
                return;

            if (typeDef.IsNested)
            {
                if (!typeDef.IsNestedPublic)
                {
                    typeDef.IsNestedPublic = true;
                }

                ForceTypeAsPublicRecurse(typeDef.DeclaringType);
            }
            else if (!typeDef.IsPublic)
            {
                typeDef.IsPublic = true;
            }
        }

        internal static void ForceTypeAsPublic(TypeDefinition typeDef)
        {
            ForceTypeAsPublicRecurse(typeDef);
        }

        internal static void ForceTypeMembersAsPublicRecurse(TypeDefinition typeDef)
        {
            if (typeDef == null)
                return;

            foreach (var field in typeDef.Fields)
            {
                if (field.IsStatic)
                    continue;

                if (!field.IsPublic)
                {
                    field.IsPublic = true;
                }

                if (!field.FieldType.IsPrimitive && !field.FieldType.IsGenericParameter)
                {
                    var fieldDef = field.FieldType.Resolve();
                    if (!fieldDef.IsEnum && field.FieldType.IsValueType)
                    {
                        ForceTypeMembersAsPublicRecurse(fieldDef);
                    }
                }
            }
        }

        internal static void ForceTypeMembersAsPublic(TypeDefinition typeDef)
        {
            ForceTypeMembersAsPublicRecurse(typeDef);
        }

        internal void WriteModifiedAssemblies(in List<TypeDefinition> typesToMakePublic)
        {
            foreach (var type in typesToMakePublic)
            {
                ForceTypeMembersAsPublic(type);
                ForceTypeAsPublic(type);
            }

            // Write out all passed in assemblies
            foreach (var asm in m_AssemblyDefs)
            {
                string asmPath = asm.MainModule.FileName;
                string asmFileName = asmPath.Substring(asmPath.LastIndexOf(Path.DirectorySeparatorChar) + 1);

                if (!m_WriteOutMscorlib)
                {
                    var stdlibNames = new[] {"mscrolib.dll", "netstandard.dll"};
                    if (stdlibNames.Any(n => n.Equals(asmFileName, StringComparison.InvariantCultureIgnoreCase)))
                        continue;
                }

                var outPath = Path.Combine(m_OutputDir, asmFileName);

                var writerParams = new WriterParameters() {WriteSymbols = asm.MainModule.HasSymbols};

                try
                {
                    asm.MainModule.Write(outPath, writerParams);
                }
                catch (DllNotFoundException e)
                {
                    /*
                     * on mac, cecil can't find ole32.dll, so just copy it.
                     */
                    if (!e.Message.Contains("ole32.dll"))
                        throw e;
                    File.Copy(asm.MainModule.FileName, outPath, true);
                    if (asm.MainModule.HasSymbols)
                    {
                        Console.WriteLine(
                            $"Warning: TypeRegGen could not write new '{asm.MainModule.Name}'. Copying original instead.");
                        File.Copy(
                            Path.ChangeExtension(asm.MainModule.FileName, "pdb"),
                            Path.ChangeExtension(outPath, "pdb"),
                            true);
                    }
                }
            }
        }

// This code needs to be ported to an ILPostProcessor and is left here for reference
#if false
        private void SaveDebugMeta(TypeGenInfoList typeGenInfoList, List<TypeDefinition> systemList)
        {
            System.Text.StringBuilder build = new System.Text.StringBuilder();
            StringWriter sw = new StringWriter(build);

            using (JsonTextWriter jw = new JsonTextWriter(sw))
            {
                jw.Formatting = Formatting.Indented;

                using (var closeA = jw.WriteStartObjectAuto())
                {
                    jw.WritePropertyName("TypeGenInfoList");
                    using (var closeB = jw.WriteStartObjectAuto())
                    {
                        for (int i = 0; i < typeGenInfoList.Count; i++)
                        {
                            var typeGenInfo = typeGenInfoList[i];

                            if (typeGenInfo.TypeDefinition == null)
                                jw.WritePropertyName("null");
                            else
                                jw.WritePropertyName(typeGenInfo.TypeDefinition.Name);
                            using (var closeC = jw.WriteStartObjectAuto())
                            {
                                // Implicit info
                                jw.WriteNameValue("Element", i);

                                // In same order as written in constructor
                                jw.WriteNameValue("TypeIndex", typeGenInfo.TypeIndex);
                                jw.WriteNameValue("TypeCategory", typeGenInfo.TypeCategory.ToString());
                                jw.WriteNameValue("EntityOffsets.Count", typeGenInfo.EntityOffsets.Count);
                                jw.WriteNameValue("EntityOffsetIndex", typeGenInfo.EntityOffsetIndex);
                                jw.WriteNameValue("MemoryOrdering", typeGenInfo.MemoryOrdering);
                                jw.WriteNameValue("StableHash", typeGenInfo.StableHash);
                                jw.WriteNameValue("BufferCapacity", typeGenInfo.BufferCapacity);
                                jw.WriteNameValue("SizeInChunk", typeGenInfo.SizeInChunk);
                                jw.WriteNameValue("ElementSize", typeGenInfo.ElementSize);
                                jw.WriteNameValue("Alignment", typeGenInfo.Alignment);
                                jw.WriteNameValue("MaxChunkCapacity", typeGenInfo.MaxChunkCapacity);
                                jw.WriteNameValue("WriteGroupTypeIndices.Count", typeGenInfo.WriteGroupTypeIndices.Count);
                                jw.WriteNameValue("WriteGroupsIndex", typeGenInfo.WriteGroupsIndex);
                                jw.WriteNameValue("BlobAssetRefOffsets.Count", typeGenInfo.BlobAssetRefOffsets.Count);
                                jw.WriteNameValue("BlobAssetRefOffsetIndex", typeGenInfo.BlobAssetRefOffsetIndex);
                                jw.WriteNameValue("FastEqualityIndex", 0);
                            }
                        }
                    }

                    jw.WritePropertyName("SystemList");
                    using (var closeB = jw.WriteStartObjectAuto())
                    {
                        for (int i = 0; i < systemList.Count; i++)
                        {
                            var sys = systemList[i];
                            jw.WritePropertyName(sys.Name);
                            using (var closeC = jw.WriteStartObjectAuto())
                            {
                                // Implicit info
                                jw.WriteNameValue("SystemIndex", i);

                                jw.WritePropertyName("UpdateInGroup");
                                using (var closeD = jw.WriteStartArrayAuto())
                                {
                                    var uig = sys.CustomAttributes.Where(t => t.AttributeType.FullName == "Unity.Entities.UpdateInGroupAttribute");
                                    foreach (var attr in uig)
                                    {
                                        if (attr.ConstructorArguments.Count != 1)
                                            jw.WriteValue("[ERROR: Wrong number of constructor arguments]");
                                        else
                                            jw.WriteValue(attr.ConstructorArguments[0].Value.ToString());
                                    }
                                }

                                jw.WritePropertyName("UpdateAfter");
                                using (var closeD = jw.WriteStartArrayAuto())
                                {
                                    var uaa = sys.CustomAttributes.Where(t => t.AttributeType.FullName == "Unity.Entities.UpdateAfterAttribute");
                                    foreach (var attr in uaa)
                                    {
                                        if (attr.ConstructorArguments.Count != 1)
                                            jw.WriteValue("[ERROR: Wrong number of constructor arguments]");
                                        else
                                            jw.WriteValue(attr.ConstructorArguments[0].Value.ToString());
                                    }
                                }

                                jw.WritePropertyName("UpdateBefore");
                                using (var closeD = jw.WriteStartArrayAuto())
                                {
                                    var uba = sys.CustomAttributes.Where(t => t.AttributeType.FullName == "Unity.Entities.UpdateBeforeAttribute");
                                    foreach (var attr in uba)
                                    {
                                        if (attr.ConstructorArguments.Count != 1)
                                            jw.WriteValue("[ERROR: Wrong number of constructor arguments]");
                                        else
                                            jw.WriteValue(attr.ConstructorArguments[0].Value.ToString());
                                    }
                                }
                            }
                        }
                    }

                    jw.WritePropertyName("JobList");
                    using (jw.WriteStartObjectAuto())
                    {
                        for (int i = 0; i < JobDescList.Count; i++)
                        {
                            var jobDesc = JobDescList[i];

                            jw.WritePropertyName(jobDesc.JobInterface.FullName);
                            using (jw.WriteStartObjectAuto())
                            {
                                jw.WriteNameValue("Producer", jobDesc.JobProducer.FullName);
                                jw.WriteNameValue("Interface", jobDesc.JobInterface.FullName);
                                jw.WriteNameValue("JobData", jobDesc.JobData.FullName);
                                if (jobDesc.JobWrapperField != null)
                                    jw.WriteNameValue("JobDataField", jobDesc.JobWrapperField.Name);
                            }
                        }
                    }
                }
            }

            var outPath = Path.Combine(m_OutputDir, m_TypeRegAssembly.Name.Name) + ".json";
            System.Console.WriteLine("Logging to " + outPath);
            System.IO.File.WriteAllText(outPath, build.ToString());
        }

#endif

        internal static void EmitLoadConstant(ref ILProcessor il, int val)
        {
            if (val >= -128 && val < 128)
            {
                switch (val)
                {
                    case -1:
                        il.Emit(OpCodes.Ldc_I4_M1);
                        break;
                    case 0:
                        il.Emit(OpCodes.Ldc_I4_0);
                        break;
                    case 1:
                        il.Emit(OpCodes.Ldc_I4_1);
                        break;
                    case 2:
                        il.Emit(OpCodes.Ldc_I4_2);
                        break;
                    case 3:
                        il.Emit(OpCodes.Ldc_I4_3);
                        break;
                    case 4:
                        il.Emit(OpCodes.Ldc_I4_4);
                        break;
                    case 5:
                        il.Emit(OpCodes.Ldc_I4_5);
                        break;
                    case 6:
                        il.Emit(OpCodes.Ldc_I4_6);
                        break;
                    case 7:
                        il.Emit(OpCodes.Ldc_I4_7);
                        break;
                    case 8:
                        il.Emit(OpCodes.Ldc_I4_8);
                        break;
                    default:
                        il.Emit(OpCodes.Ldc_I4_S, (sbyte)val);
                        break;
                }
            }
            else
            {
                il.Emit(OpCodes.Ldc_I4, val);
            }
        }

        internal static void PushNewArray(ref ILProcessor il, TypeReference elementTypeRef, int arraySize)
        {
            EmitLoadConstant(ref il, arraySize);        // Push Array Size
            il.Emit(OpCodes.Newarr, elementTypeRef);    // Push array reference to top of stack
        }

        /// <summary>
        /// NOTE: This functions assumes the array is at the top of the stack
        /// </summary>
        internal static void PushNewArrayElement(ref ILProcessor il, int elementIndex)
        {
            il.Emit(OpCodes.Dup);                   // Duplicate top of stack (the array)
            EmitLoadConstant(ref il, elementIndex); // Push array index onto the stack
        }

        internal static MethodReference MakeGenericMethodSpecialization(MethodReference self, params TypeReference[] arguments)
        {
            if (self.GenericParameters.Count != arguments.Length)
                throw new ArgumentException();

            var instance = new GenericInstanceMethod(self);
            foreach (var argument in arguments)
                instance.GenericArguments.Add(argument);

            return instance;
        }

        internal static TypeReference MakeGenericTypeSpecialization(TypeReference self, params TypeReference[] arguments)
        {
            if (self.GenericParameters.Count != arguments.Length)
                throw new ArgumentException();

            var instance = new GenericInstanceType(self);
            foreach (var argument in arguments)
                instance.GenericArguments.Add(argument);

            return instance;
        }

        internal static FieldReference MakeGenericFieldSpecialization(FieldReference self, params TypeReference[] arguments)
        {
            var instance = MakeGenericTypeSpecialization(self.DeclaringType, arguments);
            return new FieldReference(self.Name, self.FieldType, instance);
        }

        int m_ArchBits;
        bool m_BurstEnabled;
        string m_OutputDir;
        InterfaceGen m_interfaceGen;

        AssemblyDefinition m_MscorlibAssembly;
        AssemblyDefinition m_EntityAssembly;
        List<AssemblyDefinition> m_AssemblyDefs;
        bool m_WriteOutMscorlib = true;
    }
}
