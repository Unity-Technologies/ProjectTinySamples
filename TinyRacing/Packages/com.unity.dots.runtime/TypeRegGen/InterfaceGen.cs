using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.Entities.BuildUtils;
using Unity.Cecil.Awesome;
using static Unity.ZeroPlayer.CecilUtils;
using static Unity.ZeroPlayer.JobCecilUtils;

namespace Unity.ZeroPlayer
{
    class InterfaceGen
    {
        // Note that these can use nameof(Type.Func) when we switch to the ILPP approach.
        // Placed on the IJobBase instance:
        const string k_PrepareJobAtScheduleTimeFn = "PrepareJobAtScheduleTimeFn_Gen";
        const string k_PrepareJobAtExecuteTimeFn = "PrepareJobAtExecuteTimeFn_Gen";
        const string k_CleanupJobFn = "CleanupJobFn_Gen";
        const string k_GetExecuteMethodFn = "GetExecuteMethod_Gen";
        const string k_GetUnmanagedJobSizeFn = "GetUnmanagedJobSize_Gen";
        const string k_GetMarshalToBurstMethodFn = "GetMarshalToBurstMethod_Gen";
        const string k_GetMarshalFromBurstMethodFn = "GetMarshalFromBurstMethod_Gen";
        const string k_IsBursted = "IsBursted_Gen";
        const string k_PatchMinMax = "PatchMinMax_Gen";

        // Placed on the JobProducer
        const string k_ProducerExecuteFn = "ProducerExecuteFn_Gen";
        const string k_ProducerScheduleFn = "ProducerScheduleFn_Gen";
        const string k_ProducerCleanupFn = "ProducerCleanupFn_Gen";
        const string k_ProducerPatchMinMaxFn = "ProducerPatchMinMaxFn_Gen";

        const int k_ExecuteJobParam = 0;
        const int k_ExecuteJobIndexParam = 4;

        // Synchronize with JobMetaData!
        const int k_JobMetaDataIsParallelOffset = 16;
        const int k_JobMetaDataJobSize = 20;
        const int k_JobMetaDataDeferredDataPtr = 24;
        const int k_JobMetaDataManagedPtr = 32;
        const int k_JobMetaDataUnmanagedPtr = 40;

        // Synchronize with low level job system!
        const int k_ProducerScheduleReturnValue = 4;
        const int k_UserScheduleReturnValue = 2;

        List<AssemblyDefinition> m_Assemblies;

        // Known references (see ctor)
        readonly AssemblyDefinition m_SystemAssembly;
        readonly AssemblyDefinition m_ZeroJobsAssembly;
        readonly AssemblyDefinition m_LowLevelAssembly;
        readonly TypeDefinition m_SafetyHandleDef; // if null, then a release build (no safety handles)
        readonly TypeDefinition m_DisposeSentinelDef; // if null, then a release build (no safety handles)
        readonly MethodDefinition m_IJobBase_PrepareJobAtScheduleTimeFnDef;
        readonly MethodDefinition m_IJobBase_PrepareJobAtExecuteTimeFnDef;
        readonly MethodDefinition m_IJobBase_CleanupJobFnDef;
        readonly MethodDefinition m_IJobBase_IsBurstedFnDef;
        readonly MethodDefinition m_IJobBase_PatchMinMaxFnDef;
        readonly MethodDefinition m_IJobBase_GetExecuteMethodFnDef;
        readonly MethodDefinition m_IJobBase_GetUnmanagedJobSizeFnDef;
        readonly MethodDefinition m_IJobBase_GetMarshalToBurstMethodFnDef;
        readonly MethodDefinition m_IJobBase_GetMarshalFromBurstMethodFnDef;
        readonly TypeDefinition m_IJobBaseDef;
        readonly TypeDefinition m_UnsafeUtilityDef;
        readonly TypeDefinition m_JobsUtilityDef;
        readonly TypeDefinition m_PinvokeCallbackAttribute;
        readonly TypeDefinition m_JobMetaDataDef;
        readonly TypeDefinition m_JobRangesDef;
        readonly MethodDefinition m_SafetyHandle_ReleaseFnDef;
        readonly MethodDefinition m_SafetyHandle_PatchLocalFnDef;
        readonly MethodDefinition m_SafetyHandle_AllowWriteOnlyFnDef;
        readonly MethodDefinition m_SafetyHandle_AllowReadOnlyFnDef;
        readonly MethodDefinition m_SafetyHandle_UnpatchLocalFnDef;
        readonly MethodDefinition m_DisposeSentinel_ClearFnDef;
        readonly TypeDefinition m_IntPtrDef;
        readonly MethodDefinition m_IntPtr_CtorFnDef;
        readonly MethodDefinition m_JobsUtility_CountFromDeferredDataFnDef;
        readonly TypeDefinition m_JobsUtility_MinMaxDef;
        readonly FieldDefinition m_JobRanges_ArrayLengthFieldDef;
        readonly TypeDefinition m_JobsUtility_ManagedJobDelegateDef;
        readonly MethodDefinition m_JobsUtility_ManagedJobDelegate_CtorFnDef;
        readonly TypeDefinition m_JobsUtility_ManagedJobForEachDelegateDef;

        // types that are found that support DeferredConvertListToArray.  It's really only ever going to
        // be NativeArray.
        readonly Dictionary<TypeDefinition, MethodReference> m_TypesSupportingDeferredConvertListToArray = new Dictionary<TypeDefinition, MethodReference>();
        readonly HashSet<TypeDefinition> m_TypesSupportingDeallocateOnJobCompletion = new HashSet<TypeDefinition>();

        // IJobForEach re-uses types, and we don't want to multiply patch. This just records work done so it isn't re-done.
        HashSet<string> m_Patched = new HashSet<string>();    // TODO delete

        public List<JobDesc> JobDescList = new List<JobDesc>();
        public List<TypeDefinition> TypesToMakePublic = new List<TypeDefinition>();

        public class JobDesc
        {
            // Type of the producer: CustomJobProcess.
            public TypeReference JobProducer;
            // Just the Resolve() of above; use all the time.
            public TypeDefinition JobProducerDef;
            // Type of the job: ICustomJob
            public TypeDefinition JobInterface;
            // Type of the JobData, which is the first parameter of
            // the Execute: CustomJobData<T>
            // (Where T, remember, is an ICustomJob)
            public TypeReference JobData;
            // If the jobs wraps an inner definition, it is here. (Or null if not.)
            public FieldDefinition JobWrapperField;
        }

        // Performs the many assorted tasks to allow Jobs (Custom, Unity, etc.)
        // to run without reflection. The name refers to creating the IJobBase
        // interface (and code-gen of the appropriate methods) for all Jobs.
        public InterfaceGen(List<AssemblyDefinition> assemblies, bool burstEnabled)
        {
            m_Assemblies = assemblies;
            m_SystemAssembly = assemblies.First(asm => asm.Name.Name == "mscorlib");
            m_ZeroJobsAssembly = assemblies.First(asm => asm.Name.Name == "Unity.ZeroJobs");
            m_LowLevelAssembly = assemblies.First(asm => asm.Name.Name == "Unity.LowLevel");

            m_UnsafeUtilityDef = m_LowLevelAssembly.MainModule.GetAllTypes().First(i =>
                i.FullName == "Unity.Collections.LowLevel.Unsafe.UnsafeUtility");

            m_PinvokeCallbackAttribute = m_ZeroJobsAssembly.MainModule.Types.First(i =>
                i.FullName == "Unity.Jobs.MonoPInvokeCallbackAttribute");
            m_JobMetaDataDef = m_ZeroJobsAssembly.MainModule.Types.First(i =>
                i.FullName == "Unity.Jobs.LowLevel.Unsafe.JobMetaData");

            m_JobRangesDef = m_ZeroJobsAssembly.MainModule.Types.First(i =>
                i.FullName == "Unity.Jobs.LowLevel.Unsafe.JobRanges");
            m_JobRanges_ArrayLengthFieldDef = m_JobRangesDef.Fields.First(m => m.Name == "ArrayLength");

            m_JobsUtilityDef = m_ZeroJobsAssembly.MainModule.Types.First(i =>
                i.FullName == "Unity.Jobs.LowLevel.Unsafe.JobsUtility");
            m_JobsUtility_CountFromDeferredDataFnDef = m_JobsUtilityDef.Methods.First(m => m.Name == "CountFromDeferredData");
            m_JobsUtility_CountFromDeferredDataFnDef.IsPublic = true;
            m_JobsUtility_MinMaxDef = m_JobsUtilityDef.NestedTypes.First(n => n.Name == "MinMax");
            m_JobsUtility_ManagedJobDelegateDef = m_JobsUtilityDef.NestedTypes.First(i => i.Name == "ManagedJobDelegate");
            m_JobsUtility_ManagedJobDelegate_CtorFnDef = m_JobsUtility_ManagedJobDelegateDef.Methods[0];
            m_JobsUtility_ManagedJobForEachDelegateDef = m_JobsUtilityDef.NestedTypes.First(i => i.Name == "ManagedJobForEachDelegate");

            // Safety system; might not be present in the build if compiled out!
            m_SafetyHandleDef = m_ZeroJobsAssembly.MainModule.Types.FirstOrDefault(i =>
                i.FullName == "Unity.Collections.LowLevel.Unsafe.AtomicSafetyHandle");
            if (m_SafetyHandleDef != null)
            {
                m_SafetyHandle_ReleaseFnDef = m_SafetyHandleDef.Methods.First(i => i.Name == "Release");
                m_SafetyHandle_PatchLocalFnDef = m_SafetyHandleDef.Methods.First(i => i.Name == "PatchLocal");
                m_SafetyHandle_AllowWriteOnlyFnDef = m_SafetyHandleDef.Methods.First(i => i.Name == "SetAllowWriteOnly");
                m_SafetyHandle_AllowReadOnlyFnDef = m_SafetyHandleDef.Methods.First(i => i.Name == "SetAllowReadOnly");
                m_SafetyHandle_UnpatchLocalFnDef = m_SafetyHandleDef.Methods.First(i => i.Name == "UnpatchLocal");
            }

            // Safety system; might not be present in the build if compiled out!
            m_DisposeSentinelDef = m_ZeroJobsAssembly.MainModule.Types.FirstOrDefault(i =>
                i.FullName == "Unity.Collections.LowLevel.Unsafe.DisposeSentinel");
            if (m_DisposeSentinelDef != null)
            {
                m_DisposeSentinel_ClearFnDef = m_DisposeSentinelDef.Methods.First(i => i.Name == "Clear");
            }

            m_IntPtrDef = m_SystemAssembly.MainModule.Types.First(i => i.FullName == "System.IntPtr");
            m_IntPtr_CtorFnDef = m_IntPtrDef.GetConstructors().First(c => c.Parameters.Count == 1 && c.Parameters[0].ParameterType.FullName == "System.Int32");

            m_IJobBaseDef = m_ZeroJobsAssembly.MainModule.GetAllTypes().First(i => i.FullName == "Unity.Jobs.IJobBase");
            m_IJobBaseDef.IsPublic = true;

            m_IJobBase_PrepareJobAtScheduleTimeFnDef = m_IJobBaseDef.Methods.First(m => m.Name == k_PrepareJobAtScheduleTimeFn);
            m_IJobBase_PrepareJobAtExecuteTimeFnDef = m_IJobBaseDef.Methods.First(m => m.Name == k_PrepareJobAtExecuteTimeFn);
            m_IJobBase_CleanupJobFnDef = m_IJobBaseDef.Methods.First(m => m.Name == k_CleanupJobFn);
            m_IJobBase_IsBurstedFnDef = m_IJobBaseDef.Methods.First(m => m.Name == k_IsBursted);
            m_IJobBase_PatchMinMaxFnDef = m_IJobBaseDef.Methods.First(m => m.Name == k_PatchMinMax);
            m_IJobBase_GetExecuteMethodFnDef = m_IJobBaseDef.Methods.First(m => m.Name == k_GetExecuteMethodFn);
            m_IJobBase_GetUnmanagedJobSizeFnDef = m_IJobBaseDef.Methods.First(m => m.Name == k_GetUnmanagedJobSizeFn);
            m_IJobBase_GetMarshalToBurstMethodFnDef = m_IJobBaseDef.Methods.First(m => m.Name == k_GetMarshalToBurstMethodFn);
            m_IJobBase_GetMarshalFromBurstMethodFnDef = m_IJobBaseDef.Methods.First(m => m.Name == k_GetMarshalFromBurstMethodFn);

            FindAllInterestingTypes();
        }

        // Is the safety system enabled?  If it isn't, we won't have a type definition for safety handles.
        bool SafetySystemEnabled()
        {
            return m_SafetyHandleDef != null;
        }

        // Scans all types and pulls out interesting ones:
        // - the JobProducers and fills in the JobDesc that gives information about them
        // - types that support various attributes (DeferredConvertListToArray)
        void FindAllInterestingTypes()
        {
            foreach (var asm in m_Assemblies)
            {
                foreach (TypeDefinition type in asm.MainModule.GetAllTypes())
                {
                    // check if this type is a job producer
                    var producer = TryGetProducerType(type);
                    if (producer != null)
                    {
                        // There can be multiple Execute methods; simple check to find the required one.
                        // The required form:
                        //  public delegate void ExecuteJobFunction(ref JobStruct<T> jobStruct, System.IntPtr additionalPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);
                        var executeMethod = producer.Resolve().Methods.FirstOrDefault(n =>
                            n.Name == "Execute"
                            && n.Parameters.Count == 5
                            && n.Parameters[1].ParameterType.MetadataType == MetadataType.IntPtr
                            && n.Parameters[2].ParameterType.MetadataType == MetadataType.IntPtr
                            && n.Parameters[3].ParameterType.Name == "JobRanges&");

                        if (executeMethod == null)
                            throw new Exception($"{producer.FullName} is a JobProducer, but has no valid Execute method.");

                        JobDesc jobDesc = new JobDesc();
                        jobDesc.JobProducer = producer;
                        jobDesc.JobProducerDef = producer.Resolve();
                        jobDesc.JobInterface = type;

                        TypeReference jobData = executeMethod.Parameters[k_ExecuteJobParam].ParameterType.GetElementType();

                        jobDesc.JobData = jobData;
                        jobDesc.JobWrapperField = FindJobData(jobDesc.JobData.Resolve());
                        if (jobDesc.JobWrapperField == null)
                        {
                            jobDesc.JobData = producer;
                        }
                        else
                        {
                            TypesToMakePublic.Add(jobDesc.JobWrapperField.DeclaringType);
                        }

                        JobDescList.Add(jobDesc);
                    }

                    // check if this type supports NativeContainerSupportsDblah
                    if (type.HasNamedAttribute("NativeContainerSupportsDeferredConvertListToArray"))
                    {
                        var resolveMethod = type.Methods.FirstOrDefault(m => m.Name == "ResolveDeferredConvertListToArray");
                        if (resolveMethod == null)
                            throw new InvalidProgramException($"Type {type} has NativeContainerSupportsDeferredConvertListToArray attribute, but no required ResolveDeferredConvertListToArray method");
                        resolveMethod.IsPublic = true;
                        m_TypesSupportingDeferredConvertListToArray[type] = resolveMethod;
                        //Console.WriteLine($"Type supporting deferred data: {type.FullName}");
                    }

                    if (type.HasNamedAttribute("NativeContainerSupportsDeallocateOnJobCompletion"))
                    {
                        m_TypesSupportingDeallocateOnJobCompletion.Add(type);
                    }
                }
            }
        }

        static TypeReference TryGetProducerType(TypeDefinition type)
        {
            if (!type.IsInterface || !type.HasCustomAttributes)
                return null;

            var ca = type.CustomAttributes.FirstOrDefault(a =>
                a.AttributeType.FullName == "Unity.Jobs.LowLevel.Unsafe.JobProducerTypeAttribute");

            if (ca == null)
                return null;

            return (TypeReference)ca.ConstructorArguments[0].Value;
        }

        FieldDefinition FindJobData(TypeDefinition tr)
        {
            if (tr == null)
                return null;

            // internal struct JobStruct<T> where T : struct, IJob
            // {
            //    static IntPtr JobReflectionData;
            //    internal T JobData;                    <---- looking for this. Has the same name as the first generic.
            //
            // But some (many) jobs don't have the inner JobData; the job itself is the type.
            // So need to handle that fallback.

            return tr.Fields.FirstOrDefault(f => f.FieldType.Name == tr.GenericParameters[0].Name);
        }

        JobDesc FindJobProducer(TypeDefinition type, bool checkForDupe)
        {
            JobDesc producer = null;

            for (int i = 0; i < type.Interfaces.Count; i++)
            {
                var foundProducerDesc = JobDescList.FirstOrDefault(desc => desc.JobInterface.FullName == type.Interfaces[i].InterfaceType.FullName);

                if (foundProducerDesc != null)
                {
                    if (!checkForDupe)
                        return foundProducerDesc;

                    if (producer != null)
                        throw new Exception($"A job can only implement one of JobProducer. {type.FullName} implements both {foundProducerDesc.JobInterface.FullName} and {producer.JobInterface.FullName}");

                    producer = foundProducerDesc;
                }
            }
            return producer;
        }

        void GenerateProducerScheduleFn(ModuleDefinition module, JobDesc jobDesc)
        {
            MethodDefinition scheduleFn = new MethodDefinition(k_ProducerScheduleFn,
                MethodAttributes.Public | MethodAttributes.Static,
                module.ImportReference(typeof(int)));

            scheduleFn.Body.InitLocals = true;
            var il = scheduleFn.Body.GetILProcessor();

            if (jobDesc.JobWrapperField != null)
            {
                var paramType = jobDesc.JobWrapperField.DeclaringType.MakeGenericInstanceType(jobDesc.JobData.GenericParameters.ToArray());

                var wrapperParam = new ParameterDefinition("wrapper", ParameterAttributes.None,
                    module.ImportReference(paramType.MakeByReferenceType()));
                scheduleFn.Parameters.Add(wrapperParam);

                var genericJobData = jobDesc.JobData.MakeGenericInstanceType(jobDesc.JobData.GenericParameters.ToArray());
                var genericJobDataRef = module.ImportReference(genericJobData);

                AddSafetyIL(scheduleFn, module.Assembly, genericJobDataRef, null);
            }
            il.Emit(OpCodes.Ldc_I4, k_ProducerScheduleReturnValue);
            il.Emit(OpCodes.Ret);

            jobDesc.JobProducerDef.Methods.Add(scheduleFn);
        }

        void GenerateProducerPatchMinMaxFn(ModuleDefinition module, JobDesc jobDesc)
        {
            MethodDefinition minMaxFn = new MethodDefinition(k_ProducerPatchMinMaxFn,
                MethodAttributes.Public | MethodAttributes.Static,
                module.ImportReference(typeof(void)));

            ParameterDefinition minMaxParam = new ParameterDefinition("minMax", ParameterAttributes.None, module.ImportReference(m_JobsUtility_MinMaxDef));

            minMaxFn.Body.InitLocals = true;
            var il = minMaxFn.Body.GetILProcessor();

            if (jobDesc.JobWrapperField != null)
            {
                var paramType = jobDesc.JobWrapperField.DeclaringType.MakeGenericInstanceType(jobDesc.JobData.GenericParameters.ToArray());

                var wrapperParam = new ParameterDefinition("wrapper", ParameterAttributes.None,
                    module.ImportReference(paramType.MakeByReferenceType()));
                minMaxFn.Parameters.Add(wrapperParam);

                var genericJobData = jobDesc.JobData.MakeGenericInstanceType(jobDesc.JobData.GenericParameters.ToArray());
                var genericJobDataRef = module.ImportReference(genericJobData);

                AddMinMaxIL(minMaxFn, module.Assembly, minMaxParam, genericJobDataRef, null);
            }

            il.Emit(OpCodes.Ret);
            minMaxFn.Parameters.Add(minMaxParam);
            jobDesc.JobProducerDef.Methods.Add(minMaxFn);
        }

        void EmitJobPointers(ModuleDefinition module, ILProcessor il, ParameterDefinition metaPtrParam, VariableDefinition metaPtrVar, ParameterDefinition jobIndexParam, VariableDefinition jobDataPtr)
        {
            // void* jobDataPtr = jobMetaPtr + sizeof(JobMetaData) + jobIndex * jobMetaPtr->size
            // which is:
            // void* jobDataPtr = jobMetaPtr + sizeof(JobMetaData) + jobIndex * (*(int*)((byte*)jobMetaPtr + kJobMetaDataJobSize))
            if (metaPtrVar != null)
                il.Emit(OpCodes.Ldloc, metaPtrVar);
            else if (metaPtrParam != null)
                il.Emit(OpCodes.Ldarg, metaPtrParam);

            il.Emit(OpCodes.Sizeof, module.ImportReference(m_JobMetaDataDef));
            il.Emit(OpCodes.Add);

            if (jobIndexParam == null)
                il.Emit(OpCodes.Ldc_I4_0);
            else
                il.Emit(OpCodes.Ldarg, jobIndexParam);

            if (metaPtrVar != null)
                il.Emit(OpCodes.Ldloc, metaPtrVar);
            else if (metaPtrParam != null)
                il.Emit(OpCodes.Ldarg, metaPtrParam);
            il.Emit(OpCodes.Ldc_I4, k_JobMetaDataJobSize);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Ldind_I4);
            il.Emit(OpCodes.Mul);
            il.Emit(OpCodes.Add);

            il.Emit(OpCodes.Stloc, jobDataPtr);
        }

        void EmitFirstJobPointer(ModuleDefinition module, ILProcessor il, VariableDefinition metaPtrVar, VariableDefinition jobDataPtr)
        {
            // void* job = ptr + sizeof(JobMetaData)
            il.Emit(OpCodes.Ldloc, metaPtrVar);
            il.Emit(OpCodes.Sizeof, module.ImportReference(m_JobMetaDataDef));
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Stloc, jobDataPtr);
        }

        // Generates the ProducerExecuteFn which wraps the user Execute, to
        // pass down job data structure.
        void GenerateProducerExecuteFn(ModuleDefinition module, JobDesc jobDesc)
        {
            /*
             * types from other assemblies need to be able to reach into this type and grab its generated execute method
             * via GetExecuteMethod_Gen(), so it has to be public.
             */
            jobDesc.JobProducerDef.IsPublic = true;
            if (jobDesc.JobProducerDef.IsNested)
                jobDesc.JobProducerDef.IsNestedPublic = true;

            // ProducerExecuteFn_Gen(void* metaDataPtr, int jobIndex);
            MethodDefinition executeMethod = jobDesc.JobProducerDef.Methods.First(m => m.Name == "Execute");
            MethodDefinition executeGen = new MethodDefinition(k_ProducerExecuteFn,
                MethodAttributes.Public | MethodAttributes.Static,
                module.ImportReference(typeof(void)));

            var pInvokeCctor = m_PinvokeCallbackAttribute.GetConstructors();
            executeGen.CustomAttributes.Add(new CustomAttribute(module.ImportReference(pInvokeCctor.First())));

            var metaPtrParam = new ParameterDefinition("jobMetaPtr", ParameterAttributes.None,
                module.ImportReference(typeof(void*)));
            executeGen.Parameters.Add(metaPtrParam);

            var jobIndexParam = new ParameterDefinition("jobIndex", ParameterAttributes.None,
                module.ImportReference(typeof(int)));
            executeGen.Parameters.Add(jobIndexParam);

            var jobDataPtr = new VariableDefinition(module.ImportReference(typeof(void*)));
            executeGen.Body.Variables.Add(jobDataPtr);

            var stackJobRange = new VariableDefinition(module.ImportReference(m_JobRangesDef));
            executeGen.Body.Variables.Add(stackJobRange);

            var deferredDataPtr = new VariableDefinition(module.ImportReference(typeof(void*)));
            executeGen.Body.Variables.Add(deferredDataPtr);

            executeGen.Body.InitLocals = true;
            var il = executeGen.Body.GetILProcessor();

            EmitJobPointers(module, il, metaPtrParam, null, jobIndexParam, jobDataPtr);

            // The JobRanges are needed *per thread*. GetWorkStealingRange will modify the JobRanges
            // as it does work. Not obvious: the JobRanges are stored as the first thing in the metaData,
            // so the metaDataPtr is a pointer to the jobRanges.
            // Make a stack copy:
            il.Emit(OpCodes.Ldarg, metaPtrParam);
            il.Emit(OpCodes.Ldobj, module.ImportReference(m_JobRangesDef));
            il.Emit(OpCodes.Stloc, stackJobRange);

            // And now handle a deferred array length.
            // if ((byte*)jobMetaPtr + kJobMetaDataDeferredDataPtr != null) {
            //     ranges.ArrayLength = JobsUtility.CountFromDeferredData((byte*)jobMetaPtr + kJobMetaDataDeferredDataPtr);
            // }

            var branchTarget = Instruction.Create(OpCodes.Nop);
            il.Emit(OpCodes.Ldarg, metaPtrParam);
            il.Emit(OpCodes.Ldc_I4, k_JobMetaDataDeferredDataPtr);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Ldind_I);
            il.Emit(OpCodes.Stloc, deferredDataPtr);

            il.Emit(OpCodes.Ldloc, deferredDataPtr);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ceq);
            il.Emit(OpCodes.Brtrue, branchTarget);

            il.Emit(OpCodes.Ldloca, stackJobRange);
            il.Emit(OpCodes.Ldloc, deferredDataPtr);
            il.Emit(OpCodes.Call, module.ImportReference(m_JobsUtility_CountFromDeferredDataFnDef));
            il.Emit(OpCodes.Stfld, module.ImportReference(m_JobRanges_ArrayLengthFieldDef));

            il.Append(branchTarget);

            // Execute(ref jobData, new IntPtr(0), new IntPtr(0), ref ranges, jobIndex);
            il.Emit(OpCodes.Ldloc, jobDataPtr);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Newobj, module.ImportReference(m_IntPtr_CtorFnDef));
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Newobj, module.ImportReference(m_IntPtr_CtorFnDef));

            il.Emit(OpCodes.Ldloca, stackJobRange);
            il.Emit(OpCodes.Ldarg, jobIndexParam);
            il.Emit(OpCodes.Call,
                module.ImportReference(
                    executeMethod.MakeHostInstanceGeneric(jobDesc.JobData.GenericParameters.ToArray())));

            var returnInstruction = Instruction.Create(OpCodes.Ret);

            var cleanUp = jobDesc.JobProducerDef.Methods.First(m => m.Name == k_ProducerCleanupFn);
            il.Emit(OpCodes.Ldarg, metaPtrParam);
            il.Emit(OpCodes.Ldc_I4, k_JobMetaDataIsParallelOffset);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Ldind_I4);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ceq);
            il.Emit(OpCodes.Brfalse, returnInstruction);

            il.Emit(OpCodes.Ldarg, metaPtrParam);
            il.Emit(OpCodes.Call, module.ImportReference(
                cleanUp.MakeHostInstanceGeneric(jobDesc.JobProducer.GenericParameters.ToArray())));

            il.Append(returnInstruction);
            jobDesc.JobProducerDef.Methods.Add(executeGen);
        }

        void EmitCallFromProducerToJobBase(JobDesc jobDesc, ModuleDefinition module, MethodDefinition caller, ILProcessor il,
            VariableDefinition jobMetaPtrVar,
            MethodDefinition methodToCall,
            out VariableDefinition jobDataPtrVar, out TypeReference jobDataType)
        {
            // The UserJobData case is tricky.
            // jobData.UserJobData.CleanupTasksFn_Gen()
            // OR
            // jobData.CleanupTasksFn_Gen()
            if (jobDesc.JobWrapperField != null)
            {
                var genericJobDataRef = jobDesc.JobData.MakeGenericInstanceType(jobDesc.JobData.GenericParameters.ToArray());
                jobDataType = module.ImportReference(genericJobDataRef);

                jobDataPtrVar = new VariableDefinition(module.ImportReference(jobDataType.MakePointerType()));
                caller.Body.Variables.Add(jobDataPtrVar);

                EmitFirstJobPointer(module, il, jobMetaPtrVar, jobDataPtrVar);

                // jobData.UserJobData.CleanupJobFn_Gen()
                il.Emit(OpCodes.Ldloc, jobDataPtrVar);
                il.Emit(OpCodes.Ldflda,
                    module.ImportReference(TypeRegGen.MakeGenericFieldSpecialization(jobDesc.JobWrapperField,
                        jobDesc.JobData.GenericParameters.ToArray())));

                il.Emit(OpCodes.Constrained, jobDesc.JobProducerDef.GenericParameters[0]);
            }
            else
            {
                jobDataType = module.ImportReference(jobDesc.JobData.GenericParameters[0]);

                jobDataPtrVar = new VariableDefinition(module.ImportReference(jobDataType.MakePointerType()));
                caller.Body.Variables.Add(jobDataPtrVar);

                EmitFirstJobPointer(module, il, jobMetaPtrVar, jobDataPtrVar);

                // jobData.CleanupJobFn_Gen()
                il.Emit(OpCodes.Ldloc, jobDataPtrVar);
                il.Emit(OpCodes.Constrained, jobDataType);
            }

            // The first generic parameter is always the user Job, which is where the IJobBase has been attached.
            il.Emit(OpCodes.Callvirt, module.ImportReference(methodToCall));
        }

        void GenerateProducerCleanupFn(ModuleDefinition module, JobDesc jobDesc)
        {
            MethodDefinition cleanupFn = new MethodDefinition(k_ProducerCleanupFn,
                MethodAttributes.Public | MethodAttributes.Static,
                module.ImportReference(typeof(void)));

            var pInvokeCctor = m_PinvokeCallbackAttribute.GetConstructors();
            cleanupFn.CustomAttributes.Add(new CustomAttribute(module.ImportReference(pInvokeCctor.First())));

            var freeDef = m_UnsafeUtilityDef.Methods.First(n => n.Name == "Free");

            var metaPtrParam = new ParameterDefinition("jobMetaPtr", ParameterAttributes.None,
                module.ImportReference(typeof(void*)));
            cleanupFn.Parameters.Add(metaPtrParam);

            VariableDefinition unmanagedMetaPtrVar = new VariableDefinition(module.ImportReference(typeof(void*)));
            cleanupFn.Body.Variables.Add(unmanagedMetaPtrVar);

            VariableDefinition managedMetaPtrVar = new VariableDefinition(module.ImportReference(typeof(void*)));
            cleanupFn.Body.Variables.Add(managedMetaPtrVar);

            VariableDefinition jobMetaPtrVar = new VariableDefinition(module.ImportReference(typeof(void*)));
            cleanupFn.Body.Variables.Add(jobMetaPtrVar);

            VariableDefinition isParallelVar = new VariableDefinition(module.ImportReference(typeof(int)));
            cleanupFn.Body.Variables.Add(isParallelVar);

            cleanupFn.Body.InitLocals = true;
            var il = cleanupFn.Body.GetILProcessor();

            // void* managedPtr = (void*)(long)(*(IntPtr*)((byte*)jobMetaPtr + kJobMetaDataManagedPtr));
            il.Emit(OpCodes.Ldarg, metaPtrParam);
            il.Emit(OpCodes.Ldc_I4, k_JobMetaDataManagedPtr);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Ldind_I);
            il.Emit(OpCodes.Stloc, managedMetaPtrVar);

            // void* unmanagedPtr = (void*)(long)(*(IntPtr*)((byte*)jobMetaPtr + kJobMetaDataUnmanagedPtr));
            il.Emit(OpCodes.Ldarg, metaPtrParam);
            il.Emit(OpCodes.Ldc_I4, k_JobMetaDataUnmanagedPtr);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Ldind_I);
            il.Emit(OpCodes.Stloc, unmanagedMetaPtrVar);

            // The usual case:
            // jobMetaPtrVar = managedPtr
            il.Emit(OpCodes.Ldloc, managedMetaPtrVar);
            il.Emit(OpCodes.Stloc, jobMetaPtrVar);

            // isParallel = *(int*)((byte*)managedPtr + k_JobMetaDataIsParallelOffset)
            il.Emit(OpCodes.Ldarg, metaPtrParam);
            il.Emit(OpCodes.Ldc_I4, k_JobMetaDataIsParallelOffset);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Ldind_I4);
            il.Emit(OpCodes.Stloc, isParallelVar);

            var testFailed = Instruction.Create(OpCodes.Nop);

            // if ((isParallel == 0) && (unmanagedPtr != null))
            // {
            //    jobMetaPtr = unmanagedPtr;
            // }
            il.Emit(OpCodes.Ldloc, isParallelVar);
            il.Emit(OpCodes.Brtrue, testFailed);

            il.Emit(OpCodes.Ldloc, unmanagedMetaPtrVar);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Conv_U);
            il.Emit(OpCodes.Ceq);
            il.Emit(OpCodes.Brtrue, testFailed);

            il.Emit(OpCodes.Ldloc, unmanagedMetaPtrVar);
            il.Emit(OpCodes.Stloc, jobMetaPtrVar);

            il.Append(testFailed);

            // The UserJobData case is tricky.
            // jobData.UserJobData.CleanupTasksFn_Gen()
            // OR
            // jobData.CleanupTasksFn_Gen()
            VariableDefinition jobDataPtrVar;
            TypeReference jobDataType;
            EmitCallFromProducerToJobBase(jobDesc, module, cleanupFn, il,
                jobMetaPtrVar, m_IJobBase_CleanupJobFnDef, out jobDataPtrVar, out jobDataType);

            if (jobDesc.JobWrapperField != null)
            {
                GenCleanupSafetyIL(cleanupFn, module.Assembly, jobDesc.JobData.Resolve(), jobDataPtrVar);
                GenWrapperDeallocateIL(cleanupFn, module.Assembly, jobDesc.JobData.Resolve(), jobDataPtrVar);
            }

            // if (unmanagedMetaPtr != null)
            // {
            //     UnsafeUtility.Free(unmanagedMetaPtr, Allocator.TempJob);
            // }
            Instruction target = Instruction.Create(OpCodes.Nop);
            il.Emit(OpCodes.Ldloc, unmanagedMetaPtrVar);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Conv_U);
            il.Emit(OpCodes.Ceq);
            il.Emit(OpCodes.Brtrue, target);

            il.Emit(OpCodes.Ldloc, unmanagedMetaPtrVar);
            il.Emit(OpCodes.Ldc_I4_3); // literal value of Allocator.TempJob
            il.Emit(OpCodes.Call, module.ImportReference(freeDef));

            il.Append(target);

            // UnsafeUtility.Free(managedMetaPtr, Allocator.TempJob);
            il.Emit(OpCodes.Ldloc, managedMetaPtrVar);
            il.Emit(OpCodes.Ldc_I4_3); // literal value of Allocator.TempJob
            il.Emit(OpCodes.Call, module.ImportReference(freeDef));

            il.Emit(OpCodes.Ret);

            jobDesc.JobProducerDef.Methods.Add(cleanupFn);
        }

        // Adds the prefix and postfix calls to the Execute method.
        // For a super-simple Execute:
        // public static void Execute(...
        // {
        //      jobData.UserJobData.PrepareJobAtExecuteTimeFn_Gen(jobIndex);  <-- generated here
        //      jobData.UserJobData.Execute(ref jobData.abData);
        // }
        void PatchProducerExecute(ModuleDefinition module, JobDesc jobDesc)
        {
            MethodDefinition executeMethod = jobDesc.JobProducerDef.Methods.First(m => m.Name == "Execute");

            var bc = executeMethod.Body.Instructions;

            var il = executeMethod.Body.GetILProcessor();
            var first = bc[0];

            il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
            if (jobDesc.JobWrapperField != null)
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldflda,
                    module.ImportReference(TypeRegGen.MakeGenericFieldSpecialization(jobDesc.JobWrapperField,
                        jobDesc.JobData.GenericParameters.ToArray()))));
            il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg, executeMethod.Parameters[k_ExecuteJobIndexParam]));

            // The first generic parameter is always the user Job, which is where the IJobBase has been attached.
            il.InsertBefore(first, Instruction.Create(OpCodes.Constrained, jobDesc.JobProducerDef.GenericParameters[0]));
            il.InsertBefore(first, Instruction.Create(OpCodes.Callvirt, module.ImportReference(m_IJobBase_PrepareJobAtExecuteTimeFnDef)));
        }

        void PatchMinMaxRangeCall(ModuleDefinition module, JobDesc jobDesc)
        {
            MethodDefinition executeMethod = jobDesc.JobProducerDef.Methods.First(m => m.Name == "Execute");
            executeMethod.Body.SimplifyMacros();
            var bc = executeMethod.Body.Instructions;
            var patchMinMaxFn = m_JobsUtilityDef.Methods.First(m => m.Name == "PatchBufferMinMaxRanges");

            var minMaxVar = new VariableDefinition(module.ImportReference(m_JobsUtility_MinMaxDef));
            executeMethod.Body.Variables.Add(minMaxVar);

            Instruction call = bc.FirstOrDefault(inst => inst.OpCode == OpCodes.Call &&
                inst.Operand is MethodDefinition &&
                ((MethodDefinition)(inst.Operand)).FullName == patchMinMaxFn.FullName);

            if (call == null)    // not an error; just means PatchBufferMinMaxRanges isn't called.
                return;

            Instruction pop = call.Next;
            if (pop.OpCode != OpCodes.Pop)
                throw new Exception($"Error patching PatchBufferMinMaxRanges in {executeMethod.FullName}. Unexpected byte code.");

            // JobsUtility.PatchBufferMinMaxRanges(IntPtr.Zero, UnsafeUtility.AddressOf(ref jobParallelForProducer), begin, end - begin);
            // --> ProducerPatchMinMaxFn_Gen(minMax, ref jobParallelForProducer);
            // --> jobParallelForProducer.JobData.Patch(minMax);

            // Hybrid doesn't have the MinMax return, but DOTS Runtime does, so no one can ever use it.
            // Remove() the Pop so we can use the minMax.
            bc.Remove(pop);

            List<Instruction> instrList = new List<Instruction>();

            instrList.Add(Instruction.Create(OpCodes.Stloc, minMaxVar));

            instrList.Add(Instruction.Create(OpCodes.Ldarg, executeMethod.Parameters[0]));
            instrList.Add(Instruction.Create(OpCodes.Ldloc, minMaxVar));

            MethodDefinition producerMinMax = jobDesc.JobProducerDef.Methods.First(m => m.Name == k_ProducerPatchMinMaxFn);
            instrList.Add(Instruction.Create(OpCodes.Call, module.ImportReference(
                producerMinMax.MakeHostInstanceGeneric(jobDesc.JobProducer.GenericParameters.ToArray()))));

            // jobData.JobData.PatchMinMax_Gen()
            // OR
            // jobData.PatchMinMax_Gen()
            instrList.Add(Instruction.Create(OpCodes.Ldarg, executeMethod.Parameters[0]));
            if (jobDesc.JobWrapperField != null)
            {
                instrList.Add(Instruction.Create(OpCodes.Ldflda,
                    module.ImportReference(TypeRegGen.MakeGenericFieldSpecialization(jobDesc.JobWrapperField,
                        jobDesc.JobData.GenericParameters.ToArray()))));
            }

            instrList.Add(Instruction.Create(OpCodes.Ldloc, minMaxVar));
            instrList.Add(Instruction.Create(OpCodes.Constrained, jobDesc.JobProducerDef.GenericParameters[0]));

            // The first generic parameter is always the user Job, which is where the IJobBase has been attached.
            instrList.Add(Instruction.Create(OpCodes.Callvirt, module.ImportReference(m_IJobBase_PatchMinMaxFnDef)));

            var il = executeMethod.Body.GetILProcessor();
            il.InsertAfter(call, instrList);
            executeMethod.Body.Optimize();
        }

        // Can't just overwrite instruction with no-op, since it intermittently breaks the linked list.
        static void ConvertToNoOp(Instruction inst)
        {
            inst.OpCode = OpCodes.Nop;
            inst.Operand = null;
        }

        static void EmitCallJobBaseMethod(ModuleDefinition module, ILProcessor il, JobDesc jobDesc, MethodDefinition context, Instruction callInstruction, ParameterDefinition jobDataParam, VariableDefinition jobDataVar, MethodDefinition call)
        {
            // data.UserJobData.PrepareJobAtScheduleTimeFn_Gen()
            // OR
            // data.PrepareJobAtScheduleTimeFn_Gen()
            if (jobDesc.JobWrapperField == null)
            {
                if (context.Parameters[0].ParameterType.IsByReference)
                    il.InsertBefore(callInstruction, Instruction.Create(OpCodes.Ldarg, context.Parameters[0]));
                else
                    il.InsertBefore(callInstruction, Instruction.Create(OpCodes.Ldarga, context.Parameters[0]));
            }
            else
            {
                if (jobDataParam != null)
                {
                    il.InsertBefore(callInstruction, Instruction.Create(OpCodes.Ldarga, jobDataParam));
                }
                else
                {
                    il.InsertBefore(callInstruction, Instruction.Create(OpCodes.Ldloca, jobDataVar));
                }

                TypeDefinition userDataFD = jobDesc.JobWrapperField.DeclaringType;
                var arr = MakeGenericArgsArray(module, userDataFD, context.GenericParameters);

                il.InsertBefore(callInstruction,
                    Instruction.Create(OpCodes.Ldflda,
                        module.ImportReference(
                            TypeRegGen.MakeGenericFieldSpecialization(jobDesc.JobWrapperField, arr))));
            }

            // The first generic parameter is always the user Job, which is where the IJobBase has been attached.
            TypeReference constraintTypeRef = module.ImportReference(context.GenericParameters[0]);

            il.InsertBefore(callInstruction,
                Instruction.Create(OpCodes.Constrained, constraintTypeRef));

            il.InsertBefore(callInstruction,
                Instruction.Create(OpCodes.Callvirt, module.ImportReference(call)));
        }

        // Patches the Schedule method to add the size, and call the IJobBase.PrepareJobAtScheduleTimeFn_Gen
        void PatchJobSchedule(ModuleDefinition module, JobDesc jobDesc)
        {
            TypeDefinition parent = jobDesc.JobProducerDef.DeclaringType ?? jobDesc.JobProducerDef;

            foreach (var method in parent.Methods.Where(m => m.Body?.Instructions != null))
            {
                if (m_Patched.Contains("PatchJobSchedule" + method.FullName))
                    continue;
                m_Patched.Add("PatchJobSchedule" + method.FullName);

                method.Body.SimplifyMacros();
                var bc = method.Body.Instructions;
                Instruction lastProcessed = null;

                for (int i = 0; i < bc.Count; ++i)
                {
                    if (bc[i].OpCode != OpCodes.Call)
                        continue;
                    if (((MethodReference)bc[i].Operand).FullName.Contains(
                        "Unity.Jobs.LowLevel.Unsafe.JobsUtility/JobScheduleParameters::.ctor"))
                    {
                        if (ReferenceEquals(bc[i], lastProcessed))
                            continue;
                        lastProcessed = bc[i];

                        const int kSizeOffset = 4;
                        const int kProducerPrepareOffset = 3;
                        const int kUserPrepareOffset = 2;
                        const int kIsBurstedOffset = 1;

                        Instruction sizeOffsetInstruction = bc[i - kSizeOffset];
                        Instruction producerPrepareInstruction = bc[i - kProducerPrepareOffset];
                        Instruction userPrepareInstruction = bc[i - kUserPrepareOffset];
                        Instruction isBurstedInstruction = bc[i - kIsBurstedOffset];
                        Instruction callInstruction = bc[i];

                        // Magic flags value from the default parameter to help find the byte code.
                        if (!(sizeOffsetInstruction.OpCode == OpCodes.Ldc_I4 && (int)sizeOffsetInstruction.Operand == 0))
                            throw new Exception(
                                $"Expected to find default 0 value for size in JobScheduleParameters when processing '{method.FullName}'");
                        if (!(producerPrepareInstruction.OpCode == OpCodes.Ldc_I4 && (int)producerPrepareInstruction.Operand == 1))
                            throw new Exception($"Unexpected default value in '{method.FullName}'");
                        if (!(userPrepareInstruction.OpCode == OpCodes.Ldc_I4 && (int)userPrepareInstruction.Operand == 3))
                            throw new Exception($"Unexpected default value in '{method.FullName}'");
                        if (!(isBurstedInstruction.OpCode == OpCodes.Ldc_I4 && (int)isBurstedInstruction.Operand == 5))
                            throw new Exception($"Unexpected default value in '{method.FullName}'");

                        // Destroy the const load; they will become method calls.
                        ConvertToNoOp(sizeOffsetInstruction);
                        ConvertToNoOp(producerPrepareInstruction);
                        ConvertToNoOp(userPrepareInstruction);
                        ConvertToNoOp(isBurstedInstruction);

                        ILProcessor il = method.Body.GetILProcessor();

                        // Patch the size argument into a SizeOf
                        // Note this replaces one bytecode with another, so we haven't mutated the array/list
                        // we're working on.
                        {
                            if (jobDesc.JobWrapperField != null)
                            {
                                var arr = MakeGenericArgsArray(module, jobDesc.JobData, method.GenericParameters);
                                TypeReference tr = module.ImportReference(jobDesc.JobData.MakeGenericInstanceType(arr));
                                il.InsertAfter(sizeOffsetInstruction, Instruction.Create(OpCodes.Sizeof, module.ImportReference(tr)));
                            }
                            else
                            {
                                // Need to be careful that the 'this' param can be by reference.
                                if (method.Parameters[0].ParameterType.IsByReference)
                                    il.InsertAfter(sizeOffsetInstruction, Instruction.Create(OpCodes.Sizeof, method.Parameters[0].ParameterType.GetElementType()));
                                else
                                    il.InsertAfter(sizeOffsetInstruction, Instruction.Create(OpCodes.Sizeof, method.Parameters[0].ParameterType));
                            }
                        }

                        // TODO this block of code is inscrutable and needs to be refactored.
                        // All the little `if` cases that work on the jobDataVar/Param and the
                        // jobDataTR. Needs to be cleaned up.

                        // The jobData can be a local or a parameter; go find it.
                        ParameterDefinition jobDataParam = null;
                        VariableDefinition jobDataVar = null;
                        TypeReference jobDataTR = null;
                        {
                            // The parameter to AddressOf() is the parameter or local we want to load.
                            for (int j = i - 1; j > 0; --j)
                            {
                                if (bc[j].OpCode == OpCodes.Call &&
                                    ((MethodReference)bc[j].Operand).Name == "AddressOf")
                                {
                                    var instr = bc[j - 1];
                                    if (instr.OpCode == OpCodes.Ldarg_0)
                                    {
                                        // This case will occur if the job is passed in by reference.
                                        // Surprisingly rare, since it is arguably the best approach.
                                        jobDataParam = method.Parameters[0];
                                        jobDataTR = jobDataParam.ParameterType;
                                    }
                                    else if (instr.Operand is ParameterDefinition)
                                    {
                                        jobDataParam = (ParameterDefinition)instr.Operand;
                                        jobDataTR = jobDataParam.ParameterType;
                                    }
                                    else if (instr.Operand is VariableDefinition)
                                    {
                                        jobDataVar = (VariableDefinition)instr.Operand;
                                        jobDataTR = jobDataVar.VariableType;
                                    }

                                    break;
                                }
                            }

                            if (jobDataParam == null && jobDataVar == null)
                                throw new ArgumentException($"Expected to find AddressOf call in JobSchedule parameters while looking at `{method.FullName}'");
                        }

                        // Patch the last 2 parameters to call:
                        // JobProducer: ProducerScheduleFn_Gen
                        // UserJob:     PrepareJobAtScheduleTimeFn_Gen
                        // Add new instructions before the call:
                        {
                            // CustomJobProcess<T>.ProducerScheduleFn_Gen(ref data);    // if there is a job wrapper
                            // OR
                            // insert constant 4   // if there isn't a job wrapper or the call is unsupported.
                            if (jobDesc.JobWrapperField == null)
                            {
                                // No wrapper - just insert the constant.
                                il.InsertBefore(callInstruction, Instruction.Create(OpCodes.Ldc_I4_4));
                            }
                            else
                            {
                                var md = jobDesc.JobProducer.Resolve().Methods.First(f => f.Name == k_ProducerScheduleFn);
                                if (jobDataParam != null)
                                {
                                    il.InsertBefore(callInstruction, Instruction.Create(OpCodes.Ldarga, jobDataParam));
                                }
                                else
                                {
                                    il.InsertBefore(callInstruction, Instruction.Create(OpCodes.Ldloca, jobDataVar));
                                }

                                if (jobDataTR.IsGenericInstance)
                                {
                                    GenericInstanceType instance = (GenericInstanceType)jobDataTR;
                                    IList<TypeReference> genericArguments = instance.GenericArguments;
                                    var mdClosed = md.MakeHostInstanceGeneric(genericArguments.ToArray());
                                    il.InsertBefore(callInstruction, Instruction.Create(OpCodes.Call, module.ImportReference(mdClosed)));
                                }
                                else
                                {
                                    il.InsertBefore(callInstruction, Instruction.Create(OpCodes.Call, module.ImportReference(md)));
                                }
                            }
                        }
                        // data.UserJobData.PrepareJobAtScheduleTimeFn_Gen()
                        // OR
                        // data.PrepareJobAtScheduleTimeFn_Gen()
                        EmitCallJobBaseMethod(module, il, jobDesc, method, callInstruction, jobDataParam, jobDataVar, m_IJobBase_PrepareJobAtScheduleTimeFnDef);

                        // data.UserJobData.IsBursted()
                        // OR
                        // data.IsBursted()
                        EmitCallJobBaseMethod(module, il, jobDesc, method, callInstruction, jobDataParam, jobDataVar, m_IJobBase_IsBurstedFnDef);
                    }
                }

                method.Body.OptimizeMacros();
            }
        }

        // Patch CreateJobReflectionData to pass in the ProducerExecuteFn_Gen and ProducerCleanupFn_Gen methods.
        void PatchCreateJobReflection(ModuleDefinition module, JobDesc jobDesc)
        {
            var genExecuteMethodFnRef = module.ImportReference(m_IJobBase_GetExecuteMethodFnDef);
            var genUnmanagedJobSizeFnRef = module.ImportReference(m_IJobBase_GetUnmanagedJobSizeFnDef);
            var genMarshalMethodFnRef = module.ImportReference(m_IJobBase_GetMarshalToBurstMethodFnDef);
            var genMarshalFromMethodFnRef = module.ImportReference(m_IJobBase_GetMarshalFromBurstMethodFnDef);

            // Patch the CreateJobReflectionData to pass in ExecuteRT_Gen
            foreach (var method in jobDesc.JobProducerDef.Methods)
            {
                if (m_Patched.Contains("PatchCreateJobReflection" + method.FullName))
                    continue;
                m_Patched.Add("PatchCreateJobReflection" + method.FullName);

                var bc = method.Body.Instructions;
                Instruction lastProcessed = null;
                for (int i = 0; i < bc.Count; ++i)
                {
                    // we only patch calls to CreateJobReflectionData
                    if (bc[i].OpCode != OpCodes.Call)
                        continue;

                    if (ReferenceEquals(bc[i], lastProcessed))
                        continue;

                    if ((bc[i].Operand as MethodReference)?.FullName.StartsWith(
                        "System.IntPtr Unity.Jobs.LowLevel.Unsafe.JobsUtility::CreateJobReflectionData") != true)
                        continue;

                    lastProcessed = bc[i];

                    var typeOfUserJobStruct = jobDesc.JobProducerDef.GenericParameters[0];
                    if (!typeOfUserJobStruct.Constraints.Any(c => c.FullName == m_IJobBaseDef.FullName))
                        typeOfUserJobStruct.Constraints.Add(module.ImportReference(m_IJobBaseDef));

                    var userJobStructLocal = new VariableDefinition(typeOfUserJobStruct);
                    method.Body.Variables.Add(userJobStructLocal);

                    MethodDefinition producerExecuteMD = jobDesc.JobProducerDef.Methods.FirstOrDefault(m => m.Name == k_ProducerExecuteFn);
                    if (producerExecuteMD == null)
                        throw new ArgumentException($"Type '{jobDesc.JobProducerDef.FullName}' does not have a generated '{k_ProducerExecuteFn}' method");

                    MethodDefinition producerCleanupFn = jobDesc.JobProducerDef.Methods.First(m => m.Name == k_ProducerCleanupFn);

                    // Instruction before should be default arguments of null, -1, null, null
                    if (bc[i - 1].OpCode != OpCodes.Ldnull)
                        throw new InvalidOperationException($"Expected ldnull opcode (at position -1) in '{method.FullName}'");
                    if (bc[i - 2].OpCode != OpCodes.Ldc_I4_M1)
                        throw new InvalidOperationException($"Expected Ldc_I4_M1 opcode (at position -2) in '{method.FullName}'");
                    if (bc[i - 3].OpCode != OpCodes.Ldnull)
                        throw new InvalidOperationException($"Expected ldnull opcode (at position -3) in '{method.FullName}'");
                    if (bc[i - 4].OpCode != OpCodes.Ldnull)
                        throw new InvalidOperationException($"Expected ldnull opcode (at position -4) in '{method.FullName}'");

                    var il = method.Body.GetILProcessor();
                    var func = bc[i];

                    // Wipe out the default arguments
                    il.Remove(bc[i - 1]);
                    il.Remove(bc[i - 2]);
                    il.Remove(bc[i - 3]);
                    il.Remove(bc[i - 4]);

                    // and now replace with new parameters.
                    List<TypeReference> lst = new List<TypeReference>();
                    foreach (var g in jobDesc.JobProducerDef.GenericParameters)
                    {
                        TypeReference t = module.ImportReference(g);
                        lst.Add(t);
                    }

                    // Default initialize our local job struct
                    il.InsertBefore(func, Instruction.Create(OpCodes.Ldloca, userJobStructLocal));
                    il.InsertBefore(func, Instruction.Create(OpCodes.Initobj, typeOfUserJobStruct));

                    // call IJob.GetExecuteMethod_Gen()
                    il.InsertBefore(func, Instruction.Create(OpCodes.Ldloca, userJobStructLocal));
                    il.InsertBefore(func, Instruction.Create(OpCodes.Constrained, typeOfUserJobStruct));
                    il.InsertBefore(func, Instruction.Create(OpCodes.Callvirt, genExecuteMethodFnRef));

                    // ManagedJobForEachDelegate codegenCleanupDelegate
                    //
                    il.InsertBefore(func, Instruction.Create(OpCodes.Ldnull));
                    var closedProducerCleanupFn = producerCleanupFn.MakeHostInstanceGeneric(lst.ToArray());
                    il.InsertBefore(func, Instruction.Create(OpCodes.Ldftn, module.ImportReference(closedProducerCleanupFn)));
                    il.InsertBefore(func, Instruction.Create(OpCodes.Newobj, module.ImportReference(m_JobsUtility_ManagedJobDelegate_CtorFnDef)));

                    // call IJob.GetUnmanagedJobSize_Gen()
                    il.InsertBefore(func, Instruction.Create(OpCodes.Ldloca, userJobStructLocal));
                    il.InsertBefore(func, Instruction.Create(OpCodes.Constrained, typeOfUserJobStruct));
                    il.InsertBefore(func, Instruction.Create(OpCodes.Callvirt, genUnmanagedJobSizeFnRef));

                    // call IJob.GetMarshalMethod_Gen()
                    il.InsertBefore(func, Instruction.Create(OpCodes.Ldloca, userJobStructLocal));
                    il.InsertBefore(func, Instruction.Create(OpCodes.Constrained, typeOfUserJobStruct));
                    il.InsertBefore(func, Instruction.Create(OpCodes.Callvirt, genMarshalMethodFnRef));
                }
            }
        }

        public void PatchJobsCode()
        {
            foreach (JobDesc jobDesc in JobDescList)
            {
                var module = jobDesc.JobInterface.Module;

                GenerateProducerScheduleFn(module, jobDesc);
                GenerateProducerCleanupFn(module, jobDesc);
                // ExecuteFn calls CleanupFn, so generate CleanupFn first.
                GenerateProducerExecuteFn(module, jobDesc);
                GenerateProducerPatchMinMaxFn(module, jobDesc);

                PatchProducerExecute(module, jobDesc);
                PatchJobSchedule(module, jobDesc);
                PatchCreateJobReflection(module, jobDesc);
                PatchMinMaxRangeCall(module, jobDesc);
            }
        }

        public void InjectBurstInfrastructureMethods()
        {
            foreach (var asm in m_Assemblies)
            {
                var allTypes = asm.MainModule.GetAllTypes();
                foreach (var type in allTypes)
                {
                    if (type.IsStructWithInterface("Unity.Jobs.IJobBase"))
                    {
                        bool found = false;
                        List<TypeReference> args = new List<TypeReference> { type };

                        for (int i = 0; i < type.Interfaces.Count; i++)
                        {
                            foreach (JobDesc job in JobDescList)
                            {
                                if (type.Interfaces[i].InterfaceType.FullName == job.JobInterface.FullName)
                                {
                                    var producerExecuteFn = job.JobProducerDef.Methods.First(m => m.Name == k_ProducerExecuteFn);
                                    type.Methods.Add(GenGetExecuteMethodMethod(asm, type, producerExecuteFn, args));
                                    type.Methods.Add(GenGetUnmanagedJobSizeMethodMethod(asm, type));
                                    GenGetMarshalMethodMethods(asm, type);

                                    found = true;
                                    break;
                                }
                            }
                        }

                        if (!found) throw new Exception($"Could not match job {type.FullName} to a known job interface.");
                    }
                }
            }
        }

        public void AddMethods()
        {
            // All types that get IJobBase.  This is a sanity check that can just go away, because
            // I don't think this can actually ever happen.
            var typesGainedIJobBase = new HashSet<TypeDefinition>();

            // Add the IJobBase interface to the custom job interface
            foreach (JobDesc job in JobDescList)
            {
                job.JobInterface.Interfaces.Add(new InterfaceImplementation(job.JobInterface.Module.ImportReference(m_IJobBaseDef)));
                typesGainedIJobBase.Add(job.JobInterface);
            }

            // Go through each type, and if it is targeted by a JobProducer, add the IJobBase interface,
            // and the implementations of the various IJobBase methods.
            foreach (var asm in m_Assemblies)
            {
                foreach (var type in asm.MainModule.GetAllTypes())
                {
                    if (!type.HasInterfaces || !type.IsValueType)
                        continue;

                    // already saw it?
                    if (typesGainedIJobBase.Contains(type))
                    {
                        throw new InvalidOperationException($"How am I seeing {type.FullName} more than once");
                    }

                    JobDesc jobDesc = FindJobProducer(type, true);

                    if (jobDesc != null)
                    {
                        if (jobDesc != null && type.HasGenericParameters)
                        {
                            Console.WriteLine(
                                $"Warning: Job Type {type} has generic parameters.  " +
                                "This is not supported in DOTS Runtime and will be a hard error in the very near future.  " +
                                "THINGS WILL BREAK if jobs of this type are scheduled.");
                        }

                        type.Interfaces.Add(
                            new InterfaceImplementation(type.Module.ImportReference(m_IJobBaseDef)));
                        type.Methods.Add(GenScheduleMethod(asm, type));
                        type.Methods.Add(GenExecuteMethod(asm, type));
                        type.Methods.Add(GenCleanupMethod(asm, type, null));
                        type.Methods.Add(GenIsBurstedMethod(asm, type));
                        type.Methods.Add(GenPatchMinMaxMethod(asm, type));

                        typesGainedIJobBase.Add(type);
                    }
                }
            }
        }

        MethodDefinition GenGetExecuteMethodMethod(AssemblyDefinition asm,
            TypeDefinition type,
            MethodDefinition genExecuteMethod,
            List<TypeReference> genericArgs)
        {
            var module = asm.MainModule;
            var managedJobDelegate = module.ImportReference(m_JobsUtility_ManagedJobForEachDelegateDef);
            var managedJobDelegateCtor = module.ImportReference(managedJobDelegate.Resolve().Methods[0]);
            var method = new MethodDefinition(k_GetExecuteMethodFn,
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                managedJobDelegate);
            method.Body.InitLocals = true;
            var il = method.Body.GetILProcessor();

            /*
             * return "wrapper job struct, e.g. IJobExtensions.JobStruct<YourSpecificJobType>".ProducerExecuteFn_Gen;
             */

            il.Emit(OpCodes.Ldnull);
            /*
             * the clr will complain if we try to load a type with an unimplemented method on it, so generate one that
             * throws an exception.
             *
             * (il2cpp will explode if we try to just return one with null in it)
             */
            if (genExecuteMethod == null)
            {
                il.Emit(OpCodes.Throw);
            }
            else
            {
                TypeReference job = type;
                if (job.HasGenericParameters)
                {
                    job = job.MakeGenericInstanceType(job.GenericParameters.Select(p => job.Module.ImportReference(p)).ToArray());
                    // We just closed our own type, which is also the first element of the generic array.
                    genericArgs[0] = job;
                }

                // The generic args coming in are from the Execute method signature, but for purposes of adding generic params
                // to our ExecuteMethod itself, we only care about the concrete type, not whether the argument was passed by
                // reference or not since leaving the arguments as byreference will invalide the generic signature for the  method
                for (int i = 0; i < genericArgs.Count; ++i)
                {
                    var ga = genericArgs[i];
                    if (ga.FullName.StartsWith("Unity.Entities.DynamicBuffer"))
                    {
                        var gi = ga as GenericInstanceType;
                        genericArgs[i] = module.ImportReference(gi.GenericArguments[0].Resolve());
                    }
                    else if (ga.IsByReference)
                    {
                        genericArgs[i] = module.ImportReference(ga.Resolve());
                    }
                }

                MethodReference ftn = module.ImportReference(genExecuteMethod).MakeHostInstanceGeneric(genericArgs.ToArray());
                il.Emit(OpCodes.Ldftn, ftn);
                il.Emit(OpCodes.Newobj, managedJobDelegateCtor);
                il.Emit(OpCodes.Ret);
            }

            method.Body.Optimize();
            return method;
        }

        MethodDefinition GenGetUnmanagedJobSizeMethodMethod(AssemblyDefinition asm, TypeDefinition type)
        {
            var method = new MethodDefinition(k_GetUnmanagedJobSizeFn,
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                asm.MainModule.ImportReference(typeof(int)));
            var il = method.Body.GetILProcessor();

            // The implementation here will be overriden if bursted
            il.Emit(OpCodes.Ldc_I4, -1);
            il.Emit(OpCodes.Ret);

            return method;
        }

        void GenGetMarshalMethodMethods(AssemblyDefinition asm, TypeDefinition type)
        {
            var managedJobMarshalDelegate = asm.MainModule.ImportReference(m_JobsUtilityDef.NestedTypes.First(i => i.Name == "ManagedJobMarshalDelegate").Resolve());
            var marshalToFn = new MethodDefinition(k_GetMarshalToBurstMethodFn,
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                managedJobMarshalDelegate);
            {
                var il = marshalToFn.Body.GetILProcessor();
                // The implementation here will be overriden if bursted
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Ret);
            }
            type.Methods.Add(marshalToFn);

            var marshalFromFn = new MethodDefinition(k_GetMarshalFromBurstMethodFn,
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                managedJobMarshalDelegate);
            {
                var il = marshalFromFn.Body.GetILProcessor();
                // The implementation here will be overriden if bursted
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Ret);
            }
            type.Methods.Add(marshalFromFn);
        }

        MethodDefinition GenIsBurstedMethod(
            AssemblyDefinition asm,
            TypeDefinition jobTypeDef)
        {
            var method = new MethodDefinition(k_IsBursted,
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                asm.MainModule.ImportReference(typeof(int)));

            bool isBursted = jobTypeDef.HasNamedAttribute("BurstCompile");

            // -------- Parameters ---------
            method.Body.InitLocals = true;
            ILProcessor il = method.Body.GetILProcessor();
            il.Emit(isBursted ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);
            method.Body.Optimize();
            return method;
        }

        MethodDefinition GenScheduleMethod(
            AssemblyDefinition asm,
            TypeDefinition jobTypeDef)
        {
            var method = new MethodDefinition(k_PrepareJobAtScheduleTimeFn,
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                asm.MainModule.ImportReference(typeof(int)));

            // -------- Parameters ---------
            method.Body.InitLocals = true;
            ILProcessor il = method.Body.GetILProcessor();

            AddSafetyIL(method, asm, jobTypeDef, null);

            // Magic number "2" is returned so that we can check (at run time) that code-gen actually occured.
            il.Emit(OpCodes.Ldc_I4, k_UserScheduleReturnValue);
            il.Emit(OpCodes.Ret);
            method.Body.Optimize();
            return method;
        }

        MethodDefinition GenPatchMinMaxMethod(
            AssemblyDefinition asm,
            TypeDefinition jobTypeDef)
        {
            var method = new MethodDefinition(k_PatchMinMax,
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                asm.MainModule.ImportReference(typeof(void)));

            TypeDefinition minMaxTD = m_JobsUtilityDef.NestedTypes.First(n => n.Name == "MinMax");
            ParameterDefinition minMaxParam = new ParameterDefinition("minMax", ParameterAttributes.None, asm.MainModule.ImportReference(minMaxTD));
            method.Parameters.Add(minMaxParam);

            // -------- Parameters ---------
            method.Body.InitLocals = true;
            ILProcessor il = method.Body.GetILProcessor();

            AddMinMaxIL(method, asm, minMaxParam, jobTypeDef, null);

            il.Emit(OpCodes.Ret);
            method.Body.Optimize();
            return method;
        }

        MethodDefinition FindDeallocate(TypeDefinition td)
        {
            var deallocMethodDef = td.Methods.FirstOrDefault(m => m.Name == "Deallocate" && m.Parameters.Count == 0);
            if (deallocMethodDef == null)
                throw new InvalidOperationException($"Expected to find method named Deallocate on {td.FullName}!");
            deallocMethodDef.IsPublic = true;
            return deallocMethodDef;
        }

        MethodDefinition GenExecuteMethod(
            AssemblyDefinition asm,
            TypeDefinition jobTypeDef)
        {
            var method = new MethodDefinition(k_PrepareJobAtExecuteTimeFn,
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                asm.MainModule.ImportReference(typeof(void)));

            // -------- Parameters ---------
            var paramJobIndex = new ParameterDefinition("jobIndex", ParameterAttributes.None,
                asm.MainModule.ImportReference(typeof(int)));
            method.Parameters.Add(paramJobIndex);

            method.Body.InitLocals = true;
            var bc = method.Body.Instructions;

            EmitPatchDeferredListArraysIL(method, asm, jobTypeDef);
            AddThreadIndexIL(method, asm, jobTypeDef);

            bc.Add(Instruction.Create(OpCodes.Ret));
            method.Body.Optimize();
            return method;
        }

        bool FieldHasDeallocOnJobCompletion(FieldDefinition field)
        {
            if (field.HasNamedAttribute("DeallocateOnJobCompletionAttribute"))
            {
                if (!m_TypesSupportingDeallocateOnJobCompletion.Contains(field.FieldType.Resolve()))
                {
                    throw new ArgumentException(
                        $"DeallocateOnJobCompletion for {field.FullName} is invalid without NativeContainerSupportsDeallocateOnJobCompletion on {field.FieldType.FullName}");
                }
                return true;
            }

            return false;
        }

        void EmitPatchDeferredListArraysIL(MethodDefinition method, AssemblyDefinition asm, TypeDefinition jobTypeDef)
        {
            var il = method.Body.GetILProcessor();

            foreach (var fieldPath in IterateJobFields(jobTypeDef))
            {
                var fieldType = fieldPath.Last().FieldType;
                var resolvedFieldType = fieldType.Resolve();

                if (m_TypesSupportingDeferredConvertListToArray.TryGetValue(resolvedFieldType, out var patchMethod))
                {
                    var importedFieldPath = fieldPath.ImportReferencesIntoAndMakeFieldPathPublic(asm);
                    var importedPatchMethod = MakeMethodRefForGenericFieldType(asm, patchMethod, fieldType);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldflda, importedFieldPath);
                    il.Emit(OpCodes.Call, importedPatchMethod);
                }
            }
        }

        void GenCleanupDeallocateIL(
            MethodDefinition method,
            AssemblyDefinition asm,
            TypeReference fieldsTypeRef)
        {
            ILProcessor il = method.Body.GetILProcessor();

            foreach (var fieldPath in IterateJobFields(fieldsTypeRef))
            {
                var field = fieldPath.Last();
                if (!FieldHasDeallocOnJobCompletion(field.Resolve()))
                    continue;

                var importedFieldPath = fieldPath.ImportReferencesIntoAndMakeFieldPathPublic(asm);

                MethodDefinition deallocateFnDef = FindDeallocate(field.FieldType.Resolve());
                var deallocateFnRef = MakeMethodRefForGenericFieldType(asm, deallocateFnDef, field.FieldType);

                if (deallocateFnRef == null)
                    throw new Exception(
                        $"{fieldsTypeRef.Name}::{field.Name} is missing a {field.FieldType.Name}::Deallocate() implementation");

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldflda, importedFieldPath);
                il.Emit(OpCodes.Call, deallocateFnRef);
            }
        }

        void GenWrapperDeallocateIL(
            MethodDefinition method,
            AssemblyDefinition asm,
            TypeDefinition jobTypeDef,
            VariableDefinition jobPtrVar)
        {
            ILProcessor il = method.Body.GetILProcessor();

            // The calling function has done the work of finding the jobVar:
            // CustomJobData<T> jobVar = *(CustomJobData<T>*)ptr;
            // Use that! Don't want to find it again.
            foreach (var fieldPath in IterateJobFields(jobPtrVar.VariableType))
            {
                var field = fieldPath.Last();
                if (!FieldHasDeallocOnJobCompletion(field.Resolve()))
                    continue;

                var importedFieldPath = fieldPath.ImportReferencesIntoAndMakeFieldPathPublic(asm);

                MethodDefinition deallocateFnDef = FindDeallocate(field.FieldType.Resolve());
                var deallocateFnRef = MakeMethodRefForGenericFieldType(asm, deallocateFnDef, field.FieldType);

                if (deallocateFnRef == null)
                    throw new Exception(
                        $"{jobTypeDef.Name}::{field.Name} is missing a {field.FieldType.Name}::Deallocate() implementation");

                il.Emit(OpCodes.Ldloc, jobPtrVar);
                il.Emit(OpCodes.Ldflda, importedFieldPath);
                il.Emit(OpCodes.Call, deallocateFnRef);
            }
        }

        MethodDefinition GenCleanupMethod(
            AssemblyDefinition asm,
            TypeDefinition jobTypeDef,
            JobDesc jobDesc)
        {
            var method = new MethodDefinition(k_CleanupJobFn,
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                asm.MainModule.ImportReference(typeof(void)));

            method.Body.InitLocals = true;
            var bc = method.Body.Instructions;

            GenCleanupDeallocateIL(method, asm, jobTypeDef);
            GenCleanupSafetyIL(method, asm, jobTypeDef, null);

            bc.Add(Instruction.Create(OpCodes.Ret));
            return method;
        }

        void AddThreadIndexIL(
            MethodDefinition method,
            AssemblyDefinition asm,
            TypeDefinition jobTypeDef)
        {
            var il = method.Body.GetILProcessor();
            var paramJobIndex = method.Parameters[0];

            foreach (var fieldPath in IterateJobFields(jobTypeDef,
                shouldYieldFilter: (field) => field.FieldType.MetadataType == MetadataType.Int32))
            {
                FieldReference field = fieldPath.Last();

                // We only care about int32 fields and only if they have the NativeSetThreadIndex attribute
                // Unity.Collections.LowLevel.Unsafe.NativeSetThreadIndexAttribute
                FieldDefinition fieldDef = field.Resolve();
                if (!fieldDef.HasNamedAttribute("NativeSetThreadIndex"))
                    continue;

                var importedFieldPath = fieldPath.ImportReferencesIntoAndMakeFieldPathPublic(asm);

                // emit code to store the thread index (from param) into the field
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldflda, importedFieldPath.GetRange(0, importedFieldPath.Count - 1));
                il.Emit(OpCodes.Ldarg, paramJobIndex);
                il.Emit(OpCodes.Stfld, importedFieldPath.Last());
            }
        }

        public static bool IsValidSafetyHandleField(FieldReference fieldRef)
        {
            return fieldRef.FieldType.FullName == "Unity.Collections.LowLevel.Unsafe.AtomicSafetyHandle" &&
                (fieldRef.Name == "m_Safety" || fieldRef.Name == "m_Safety0");
        }

        public static bool IsValidDisposeSentinelField(FieldReference fieldRef)
        {
            return fieldRef.FieldType.FullName == "Unity.Collections.LowLevel.Unsafe.DisposeSentinel";
        }

        public static bool IsValueTypeField(FieldReference fieldRef)
        {
            return fieldRef.FieldType.IsValueType && !fieldRef.FieldType.IsPrimitive;
        }

        public static bool IsPossibleSafetyCleanupTypeField(FieldReference fieldRef)
        {
            return IsValueTypeField(fieldRef) || IsValidDisposeSentinelField(fieldRef);
        }

        void AddSafetyIL(
            MethodDefinition method,
            AssemblyDefinition asm,
            TypeReference fieldsTypeRef,
            VariableDefinition variableDefinition)    // if this is null, then "this" is assumed.
        {
            if (!SafetySystemEnabled())
                return;

            TypeDefinition fieldsTypeDef = fieldsTypeRef.Resolve();

            if (!fieldsTypeDef.HasFields || !fieldsTypeDef.IsValueType)
                return;

            method.Body.InitLocals = true;
            ILProcessor il = method.Body.GetILProcessor();

            foreach (var fieldPath in IterateJobFields(fieldsTypeRef,
                shouldYieldFilter: IsPossibleSafetyCleanupTypeField))
            {
                var field = fieldPath.Last();

                // This method - AddSafetyIL - can be called on a UserJob or a wrapper.
                // If the wrapper is used, we need to be careful to not recurse in to the UserJob,
                // which will cause bad IL generation. We are spared this fate since the fieldType
                // is a generic parameter, and thus not a primitive. This is tested for in
                // IterateJobFields, which only iterates into value type fields.

                if (IsValidSafetyHandleField(field))
                {
                    // If this is a safety handle, check the parent field (if any!) for any interesting attributes
                    // that may modify how we generate safety checks
                    bool disableSafety = false;
                    bool writeOnly = false;
                    bool readOnly = false;
                    bool needRelease = false;

                    if (fieldPath.Count > 1)
                    {
                        var parentField = fieldPath[fieldPath.Count - 2];
                        var fieldTypeDef = parentField.FieldType.Resolve();
                        var fieldDef = parentField.Resolve();

                        // Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestrictionAttribute
                        disableSafety = fieldDef.HasNamedAttribute("NativeDisableContainerSafetyRestriction");

                        // Unity.Collections.WriteOnlyAttribute
                        // Unity.Collections.LowLevel.Unsafe.NativeContainerAttribute
                        // Unity.Collections.NativeContainerIsAtomicWriteOnlyAttribute
                        writeOnly = fieldDef.HasNamedAttribute("WriteOnly") ||
                            (fieldTypeDef.HasNamedAttribute("NativeContainer") && fieldTypeDef.HasNamedAttribute("NativeContainerIsAtomicWriteOnly"));

                        // Unity.Collections.ReadOnlyAttribute
                        readOnly = fieldDef.HasNamedAttribute("ReadOnly");

                        needRelease = FieldHasDeallocOnJobCompletion(fieldDef);

                        if (writeOnly && readOnly)
                        {
                            throw new ArgumentException(
                                $"[ReadOnly] and [WriteOnly] are both specified on '{fieldDef.FullName}'");
                        }
                    }

                    var importedFieldPath = fieldPath.ImportReferencesIntoAndMakeFieldPathPublic(asm);

                    // AtomicSafetyHandle.Release(result.m_Safety);
                    if (needRelease)
                    {
                        il.EmitLdThisOrVarAddress(variableDefinition); //il.Emit(OpCodes.Ldarg_0));
                        il.Emit(OpCodes.Ldfld, importedFieldPath);
                        il.Emit(OpCodes.Call, asm.MainModule.ImportReference(m_SafetyHandle_ReleaseFnDef));
                    }

                    // AtomicSafetyHandle.PatchLocal(ref result.m_Safety);
                    il.EmitLdThisOrVarAddress(variableDefinition); //il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldflda, importedFieldPath);
                    il.Emit(OpCodes.Call, asm.MainModule.ImportReference(m_SafetyHandle_PatchLocalFnDef));

                    // AtomicSafetyHandle.SetAllowWriteOnly(ref result.m_Safety);
                    // or
                    // AtomicSafetyHandle.SetAllowReadOnly(ref result.m_Safety);
                    if (!disableSafety && (writeOnly || readOnly))
                    {
                        il.EmitLdThisOrVarAddress(variableDefinition); //il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldflda, importedFieldPath);

                        if (writeOnly)
                            il.Emit(OpCodes.Call, asm.MainModule.ImportReference(m_SafetyHandle_AllowWriteOnlyFnDef));
                        if (readOnly)
                            il.Emit(OpCodes.Call, asm.MainModule.ImportReference(m_SafetyHandle_AllowReadOnlyFnDef));
                    }
                }
                else if (IsValidDisposeSentinelField(field))
                {
                    var importedFieldPath = fieldPath.ImportReferencesIntoAndMakeFieldPathPublic(asm);

                    il.EmitLdThisOrVarAddress(variableDefinition); //il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldflda, importedFieldPath);
                    il.Emit(OpCodes.Call, asm.MainModule.ImportReference(m_DisposeSentinel_ClearFnDef));
                }
            }
        }

        static bool DisablesMinMaxSafety(FieldReference fr)
        {
            return new[]
            {
                "ReadOnly",
                "NativeDisableContainerSafetyRestriction",
                "NativeDisableParallelForRestriction"
            }.Any(attrName => fr.Resolve().HasNamedAttribute(attrName));
        }

        void AddMinMaxIL(
            MethodDefinition method,
            AssemblyDefinition asm,
            ParameterDefinition minMaxParam,
            TypeReference fieldsTypeRef,
            VariableDefinition variableDefinition)    // if this is null, then "this" is assumed.
        {
            TypeDefinition fieldsTypeDef = fieldsTypeRef.Resolve();

            if (!SafetySystemEnabled())
                return;

            if (!fieldsTypeDef.HasFields || !fieldsTypeDef.IsValueType)
                return;

            method.Body.InitLocals = true;
            ILProcessor il = method.Body.GetILProcessor();

            foreach (var fieldPath in IterateJobFields(fieldsTypeRef,
                shouldYieldFilter: (field) => field.FieldType.MetadataType == MetadataType.Int32,
                shouldRecurseFilter: (field) => !DisablesMinMaxSafety(field)))
            {
                var field = fieldPath.Last();

                if (field.Name != "m_MinIndex" && field.Name == "m_MaxIndex")
                    continue;

                FieldDefinition fieldDefinition = field.Resolve();
                if (!DisablesMinMaxSafety(fieldDefinition))
                    continue;

                fieldDefinition.IsPublic = true;

                var importedHierarchy = fieldPath.ImportReferencesIntoAndMakeFieldPathPublic(asm);

                il.EmitLdThisOrVarAddress(variableDefinition);
                il.Emit(OpCodes.Ldflda, importedHierarchy.GetRange(0, importedHierarchy.Count - 1));

                // Load minmax argument, then store it in the field as needed
                il.Emit(OpCodes.Ldarg, minMaxParam);
                string fieldName = field.Name == "m_MinIndex" ? "Min" : "Max";
                var fInMinMax = minMaxParam.ParameterType.Resolve().Fields.First(f => f.Name == fieldName);
                il.Emit(OpCodes.Ldfld, asm.MainModule.ImportReference(fInMinMax));
                il.Emit(OpCodes.Stfld, asm.MainModule.ImportReference(importedHierarchy[importedHierarchy.Count - 1])); //asm.MainModule.ImportReferenceInto(field.Resolve()));
            }
        }

        void GenCleanupSafetyIL(
            MethodDefinition method,
            AssemblyDefinition asm,
            TypeReference fieldsTypeRef,
            VariableDefinition variableDefinition)    // if this is null, then "this" is assumed.
        {
            if (!SafetySystemEnabled())
                return;

            TypeDefinition fieldsTypeDef = fieldsTypeRef.Resolve();

            if (!fieldsTypeDef.HasFields || !fieldsTypeDef.IsValueType)
                return;

            method.Body.InitLocals = true;
            var il = method.Body.GetILProcessor();

            // Iterate through all value type fields
            foreach (var fieldPath in IterateJobFields(fieldsTypeRef, shouldYieldFilter: IsValueTypeField))
            {
                // and if it's a safety handle, process it
                if (IsValidSafetyHandleField(fieldPath.Last()))
                {
                    var importedFieldPath = fieldPath.ImportReferencesIntoAndMakeFieldPathPublic(asm);

                    // AtomicSafetyHandle.UnpatchLocal(ref result.m_Safety);
                    il.EmitLdThisOrVarValue(variableDefinition);
                    il.Emit(OpCodes.Ldflda, importedFieldPath);
                    il.Emit(OpCodes.Call, asm.MainModule.ImportReference(m_SafetyHandle_UnpatchLocalFnDef));
                }
            }
        }
    }

    // Jobs-specific Cecil utilities/extension methods
    public static class JobCecilUtils
    {
        public static void EmitLdThisOrVarAddress(this ILProcessor il, VariableDefinition variableDefinition)
        {
            if (variableDefinition == null)
            {
                il.Emit(OpCodes.Ldarg_0);
            }
            else
            {
                il.Emit(OpCodes.Ldloca, variableDefinition);
            }
        }

        public static void EmitLdThisOrVarValue(this ILProcessor il, VariableDefinition variableDefinition)
        {
            if (variableDefinition == null)
            {
                il.Emit(OpCodes.Ldarg_0);
            }
            else
            {
                il.Emit(OpCodes.Ldloc, variableDefinition);
            }
        }

        public static TypeReference[] MakeGenericArgsArray(ModuleDefinition module, IGenericParameterProvider forType,
            IEnumerable<GenericParameter> gp)
        {
            // We may have more generic parameters than we need. For example,
            // the schedule may take more parameters than needed by the job.
            return gp.Take(forType.GenericParameters.Count).Select(param => module.ImportReference(param)).ToArray();
        }
    }
}
