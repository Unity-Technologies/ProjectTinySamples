using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;

namespace Unity.Jobs.LowLevel.Unsafe
{
    // Code gen depends on these constants.
    public enum JobType
    {
        Single,
        ParallelFor
    }

    public enum WorkStealingState : int
    {
        NotInitialized,
        Initialized,
        Done
    }

    // NOTE: This doesn't match (big) Unity's JobRanges because JobsUtility.GetWorkStealingRange isn't fully implemented
    public struct JobRanges
    {
        public int ArrayLength;
        public int IndicesPerPhase;
        public WorkStealingState State;
        public int runOnMainThread;
    }

    // The internally used header for JobData
    // The code-gen relies on explicit layout.
    // Trivial with one entry, but declared here as a reminder.
    // Also, to preserve alignment, code-gen is using a size=16
    // for this structure.
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    unsafe struct JobMetaData
    {
        // Must be the zero offset (JobMetaDataPtr === JobRanges in InterfaceGen)
        [FieldOffset(0)]
        public JobRanges JobRanges;

        // Sync with InterfaceGen.cs!
        public const int kJobMetaDataIsParallelOffset = 16;
        [FieldOffset(kJobMetaDataIsParallelOffset)]
        public int isParallelFor;

        // Sync with InterfaceGen.cs!
        const int kJobMetaDataJobSize = 20;
        [FieldOffset(kJobMetaDataJobSize)]
        public int jobDataSize;

        // Sync with InterfaceGen.cs!
        const int kJobMetaDataDeferredDataPtr = 24;
        [FieldOffset(kJobMetaDataDeferredDataPtr)]
        public void* deferredDataPtr;

        // Sync with InterfaceGen.cs!
        const int kJobMetaDataManagedPtr = 32;
        [FieldOffset(kJobMetaDataManagedPtr)]
        public void* managedPtr;

        // Sync with InterfaceGen.cs!
        const int kJobMetaDataUnmanagedPtr = 40;
        [FieldOffset(kJobMetaDataUnmanagedPtr)]
        public void* unmanagedPtr;
    }

    public enum ScheduleMode : int
    {
        Run,
        Batched,
        RunOnMainThread
    }

    [AttributeUsage(AttributeTargets.Interface)]
    public sealed class JobProducerTypeAttribute : Attribute
    {
        public JobProducerTypeAttribute(Type producerType) => throw new NotImplementedException();
        public Type ProducerType => throw new NotImplementedException();
    }

    public static class JobsUtility
    {
#if UNITY_SINGLETHREADED_JOBS
        public const int JobWorkerCount = 0;
        public const int MaxJobThreadCount = 1;
#else
        struct JobWorkerCountKey { }
        static readonly SharedStatic<int> s_JobWorkerCount = SharedStatic<int>.GetOrCreate<JobWorkerCountKey>();

        public static unsafe int JobWorkerCount
        {
            get
            {
                return s_JobWorkerCount.Data;
            }
        }
        public const int MaxJobThreadCount = 128;
#endif
        public const int CacheLineSize = 64;

        public static bool JobCompilerEnabled => true;
        public static bool JobDebuggerEnabled => false;

        [StructLayout(LayoutKind.Sequential)]
        public struct JobScheduleParameters
        {
            public JobHandle    Dependency;
            public ScheduleMode ScheduleMode;
            public IntPtr       ReflectionData;
            public IntPtr       JobDataPtr;

            public unsafe JobScheduleParameters(void* jobData,
                IntPtr reflectionData,
                JobHandle jobDependency,
                ScheduleMode scheduleMode,
                int jobDataSize = 0,
                int producerJobSchedule = 1,
                int userJobSchedule = 3,
                int isBursted = 5)
            {
                // Synchronize with InterfaceGen.cs!
                const int k_ProducerScheduleReturnValue = 4;
                const int k_UserScheduleReturnValue = 2;

                const string k_PostFix = " Seeing this error indicates a bug in the dots compiler. We'd appreciate a bug report (About->Report a Bug...).";

                // Default is 0; code-gen should set to a correct size.
                if (jobDataSize == 0)
                    throw new InvalidOperationException("JobScheduleParameters (size) should be set by code-gen." + k_PostFix);
                if (producerJobSchedule != k_ProducerScheduleReturnValue)
                    throw new InvalidOperationException(
                        "JobScheduleParameter (which is the return code of ProducerScheduleFn_Gen) should be set by code-gen." + k_PostFix);
                if (userJobSchedule != k_UserScheduleReturnValue)
                    throw new InvalidOperationException(
                        "JobScheduleParameter (which is the return code of PrepareJobAtScheduleTimeFn_Gen) should be set by code-gen." + k_PostFix);
                if (!(isBursted == 0 || isBursted == 1))
                    throw new InvalidOperationException(
                        "JobScheduleParameter (which is the return code of RunOnMainThread_Gen) should be set by code-gen." + k_PostFix);

                int nWorkers = JobWorkerCount > 0 ? JobWorkerCount : 1;
                void* mem = AllocateJobHeapMemory(jobDataSize, nWorkers);

                // A copy of the JobData is needed *for each worker thread* as it will
                // get mutated in unique ways (threadIndex, safety.) The jobIndex is passed
                // to the Execute method, so a thread can look up the correct jobData to use.
                // Cleanup is always called on jobIndex=0.
                for(int i=0; i<nWorkers; i++)
                    UnsafeUtility.MemCpy(((byte*)mem + sizeof(JobMetaData) + jobDataSize*i), jobData, jobDataSize);
                UnsafeUtility.AssertHeap(mem);

                JobMetaData jobMetaData = new JobMetaData();
                jobMetaData.jobDataSize = jobDataSize;
                UnsafeUtility.CopyStructureToPtr(ref jobMetaData, mem);

                Dependency = jobDependency;
                JobDataPtr = (IntPtr) mem;
                ReflectionData = reflectionData;
                ScheduleMode = isBursted == 0 ? ScheduleMode.RunOnMainThread : scheduleMode;
            }
        }

        class ReflectionDataStore
        {
            // Dotnet throws an exception if the function pointers aren't pinned by a delegate.
            // Error checking? The pointers certainly can't change.
            // This class registers the function pointers with the GC.
            // TODO a more elegant solution, or switch to calli and avoid this.
            public ReflectionDataStore(Delegate executeDelegate, Delegate codeGenCleanupDelegate, Delegate codeGenExecuteDelegate, Delegate codeGenMarshalToBurstDelegate)
            {
                ExecuteDelegate = executeDelegate;
                ExecuteDelegateHandle = GCHandle.Alloc(ExecuteDelegate);

                if (codeGenCleanupDelegate != null)
                {
                    CodeGenCleanupDelegate = codeGenCleanupDelegate;
                    CodeGenCleanupDelegateHandle = GCHandle.Alloc(CodeGenCleanupDelegate);
                    CodeGenCleanupFunctionPtr = Marshal.GetFunctionPointerForDelegate(codeGenCleanupDelegate);
                }

                if (codeGenExecuteDelegate != null)
                {
                    CodeGenExecuteDelegate = codeGenExecuteDelegate;
                    CodeGenExecuteDelegateHandle = GCHandle.Alloc(CodeGenExecuteDelegate);
                    CodeGenExecuteFunctionPtr = Marshal.GetFunctionPointerForDelegate(codeGenExecuteDelegate);
                }

                if (codeGenMarshalToBurstDelegate != null)
                {
                    CodeGenMarshalToBurstDelegate = codeGenMarshalToBurstDelegate;
                    CodeGenMarshalToBurstDelegateHandle = GCHandle.Alloc(CodeGenMarshalToBurstDelegate);
                    CodeGenMarshalToBurstFunctionPtr = Marshal.GetFunctionPointerForDelegate(codeGenMarshalToBurstDelegate);
                }
            }

            internal ReflectionDataStore next;

            public Delegate ExecuteDelegate;
            public GCHandle ExecuteDelegateHandle;

            public Delegate CodeGenCleanupDelegate;
            public GCHandle CodeGenCleanupDelegateHandle;
            public IntPtr   CodeGenCleanupFunctionPtr;

            public Delegate CodeGenExecuteDelegate;
            public GCHandle CodeGenExecuteDelegateHandle;
            public IntPtr   CodeGenExecuteFunctionPtr;

            public Delegate CodeGenMarshalToBurstDelegate;
            public GCHandle CodeGenMarshalToBurstDelegateHandle;
            public IntPtr   CodeGenMarshalToBurstFunctionPtr;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ReflectionDataProxy
        {
            public JobType JobType;
            public IntPtr  ExecuteFunctionPtr;
            public IntPtr  CleanupFunctionPtr;
#if ENABLE_UNITY_COLLECTIONS_CHECKS && !UNITY_DOTSPLAYER_IL2CPP
            public int     UnmanagedSize;
            public IntPtr  MarshalToBurstFunctionPtr;
#endif
        }

        public unsafe delegate void ManagedJobDelegate(void* ptr);
        // TODO: Should be ManagedJobExecuteDelegate, but waiting for Burst update.
        public unsafe delegate void ManagedJobForEachDelegate(void* ptr, int jobIndex);
        public unsafe delegate void ManagedJobMarshalDelegate(void* dst, void* src);

        static private ReflectionDataStore reflectionDataStoreRoot = null;

#if UNITY_WINDOWS
        internal const string nativejobslib = "nativejobs";
#else
        internal const string nativejobslib = "libnativejobs";
#endif

        public static void Initialize()
        {
#if !UNITY_SINGLETHREADED_JOBS
            // We need to push the thread count before the jobs run, because we can't make a lazy
            // call to Environment.ProcessorCount from Burst.
            // about the 8 thread restriction: https://unity3d.atlassian.net/browse/DOTSR-1499
            s_JobWorkerCount.Data = Environment.ProcessorCount < 8 ? Environment.ProcessorCount : 8;
#endif
        }

#if !UNITY_SINGLETHREADED_JOBS
        // Todo: Remove this jank when nativejobs offers the ability to make a job queue without specifying a name
        static readonly byte[] JobQueueName = new byte[] { 0x6a, 0x6f, 0x62, 0x2d, 0x71, 0x75, 0x65, 0x75, 0x65, 0x00 }; // job-queue, UTF-8
        static readonly byte[] WorkerThreadName = new byte[] { 0x77, 0x6f, 0x72, 0x6b, 0x65, 0x72, 0x2d, 0x62, 0x65, 0x65, 0x00 }; // worker-bee, UTF-8
        internal static unsafe IntPtr JobQueue
        {
            get
            {
                if (s_JobQueue.Data == IntPtr.Zero)
                    s_JobQueue.Data = CreateJobQueue((IntPtr)UnsafeUtility.AddressOf(ref JobQueueName[0]), (IntPtr)UnsafeUtility.AddressOf(ref WorkerThreadName[0]), JobWorkerCount);

                return s_JobQueue.Data;
            }
        }
        internal static IntPtr BatchScheduler
        {
            get
            {
                if (s_BatchScheduler.Data == IntPtr.Zero)
                {
                    Assert.IsTrue(JobQueue != IntPtr.Zero);
                    s_BatchScheduler.Data = CreateJobBatchScheduler();
                }

                return s_BatchScheduler.Data;
            }
        }

        struct JobQueueSharedStaticKey { }
        struct BatchScheduldeSharedStaticKey { }
        static readonly SharedStatic<IntPtr> s_JobQueue = SharedStatic<IntPtr>.GetOrCreate<JobQueueSharedStaticKey>();
        static readonly SharedStatic<IntPtr> s_BatchScheduler = SharedStatic<IntPtr>.GetOrCreate<BatchScheduldeSharedStaticKey>();

        public static JobHandle ScheduleJob(IntPtr jobFuncPtr, IntPtr jobDataPtr, JobHandle dependsOn)
        {
            Assert.IsTrue(JobQueue != IntPtr.Zero);
            return ScheduleJobBatch(BatchScheduler, jobFuncPtr, jobDataPtr, dependsOn);
        }

        public static JobHandle ScheduleJobParallelFor(IntPtr jobFuncPtr, IntPtr jobCompletionFuncPtr, IntPtr jobDataPtr, int arrayLength, int innerloopBatchCount, JobHandle dependsOn)
        {
            Assert.IsTrue(JobQueue != IntPtr.Zero && BatchScheduler != IntPtr.Zero);
            return ScheduleJobBatchParallelFor(BatchScheduler, jobFuncPtr, jobDataPtr, arrayLength, innerloopBatchCount, jobCompletionFuncPtr, dependsOn);
        }

        // TODO: Need to find a good place to shut down jobs on application quit/exit
        public static void Shutdown()
        {
            if (s_BatchScheduler.Data != IntPtr.Zero)
            {
                DestroyJobBatchScheduler(s_BatchScheduler.Data);
                s_BatchScheduler.Data = IntPtr.Zero;
            }

            if (s_JobQueue.Data != IntPtr.Zero)
            {
                DestroyJobQueue();
                s_JobQueue.Data = IntPtr.Zero;
            }
        }

        [DllImport(nativejobslib)]
        static extern unsafe IntPtr CreateJobQueue(IntPtr queueName, IntPtr workerName, int numJobWorkerThreads);

        [DllImport(nativejobslib)]
        static extern void DestroyJobQueue();

        [DllImport(nativejobslib)]
        static extern IntPtr CreateJobBatchScheduler();

        [DllImport(nativejobslib)]
        static extern void DestroyJobBatchScheduler(IntPtr scheduler);

        [DllImport(nativejobslib)]
        static extern JobHandle ScheduleJobBatch(IntPtr scheduler, IntPtr func, IntPtr userData, JobHandle dependency);

        [DllImport(nativejobslib)]
        static extern JobHandle ScheduleJobBatchParallelFor(IntPtr scheduler, IntPtr func, IntPtr userData, int arrayLength, int innerloopBatchCount, IntPtr completionFunc, JobHandle dependency);

        [DllImport(nativejobslib)]
        internal static extern void ScheduleMultiDependencyJob(ref JobHandle fence, IntPtr dispatch, IntPtr dependencies, int fenceCount);

        [DllImport(nativejobslib)]
        internal static extern void ScheduleBatchedJobs(IntPtr scheduler);

        [DllImport(nativejobslib)]
        internal static extern void Complete(IntPtr scheduler, ref JobHandle jobHandle);

        [DllImport(nativejobslib)]
        internal static extern int IsCompleted(IntPtr scheduler, ref JobHandle jobHandle);

        // The following are needed regardless if we are in single or multi-threaded environment
        [DllImport(nativejobslib, EntryPoint = "IsExecutingJob")]
        internal static extern int IsExecutingJobInternal();

        public static bool IsExecutingJob() { return (IsExecutingJobInternal() != 0) || (UnsafeUtility.GetInJob() > 0); }
#else
        public static bool IsExecutingJob()
        {
            return UnsafeUtility.GetInJob() > 0;
        }
        public static void Shutdown() {}
#endif


        public static unsafe IntPtr CreateJobReflectionData(Type type, Type _, JobType jobType,
            Delegate executeDelegate,
            Delegate cleanupDelegate = null,
            ManagedJobForEachDelegate codegenExecuteDelegate = null,
            ManagedJobDelegate codegenCleanupDelegate = null,
            int codegenUnmanagedJobSize = -1,
            ManagedJobMarshalDelegate codegenMarshalToBurstDelegate = null)
        {
            // Tiny doesn't use this on any codepath currently; may need future support for custom jobs.
            Assert.IsTrue(cleanupDelegate == null, "Runtime needs support for cleanup delegates in jobs.");

            Assert.IsTrue(codegenExecuteDelegate != null, "Code gen should have supplied an execute wrapper.");
            Assert.IsTrue(jobType != JobType.ParallelFor || codegenCleanupDelegate != null, "For ParallelFor jobs, code gen should have supplied a cleanup wrapper.");
#if ENABLE_UNITY_COLLECTIONS_CHECKS && !UNITY_DOTSPLAYER_IL2CPP
            Assert.IsTrue((codegenUnmanagedJobSize != -1 && codegenMarshalToBurstDelegate != null) || (codegenUnmanagedJobSize == -1 && codegenMarshalToBurstDelegate == null), "Code gen should have supplied a marshal wrapper.");
#endif

            var reflectionDataPtr = UnsafeUtility.Malloc(UnsafeUtility.SizeOf<ReflectionDataProxy>(),
                UnsafeUtility.AlignOf<ReflectionDataProxy>(), Allocator.Persistent);

            var reflectionData = new ReflectionDataProxy();
            reflectionData.JobType = jobType;

            // Protect against garbage collector relocating delegate
            ReflectionDataStore store = new ReflectionDataStore(executeDelegate, codegenCleanupDelegate, codegenExecuteDelegate, codegenMarshalToBurstDelegate);
            store.next = reflectionDataStoreRoot;
            reflectionDataStoreRoot = store;

            reflectionData.ExecuteFunctionPtr = store.CodeGenExecuteFunctionPtr;
            if (codegenCleanupDelegate != null)
                reflectionData.CleanupFunctionPtr = store.CodeGenCleanupFunctionPtr;

#if ENABLE_UNITY_COLLECTIONS_CHECKS && !UNITY_DOTSPLAYER_IL2CPP
            reflectionData.UnmanagedSize = codegenUnmanagedJobSize;
            if(codegenUnmanagedJobSize != -1)
                reflectionData.MarshalToBurstFunctionPtr = store.CodeGenMarshalToBurstFunctionPtr;
#endif

            UnsafeUtility.CopyStructureToPtr(ref reflectionData, reflectionDataPtr);

            return new IntPtr(reflectionDataPtr);
        }

#if UNITY_SINGLETHREADED_JOBS
        public static int GetDefaultIndicesPerPhase(int arrayLength)
        {
            return Math.Max(arrayLength, 1);
        }
#else
        public static int GetDefaultIndicesPerPhase(int arrayLength)
        {
            return (JobWorkerCount > 0) ? Math.Max((arrayLength + (JobWorkerCount - 1)) / JobWorkerCount, 1) : 1;
        }
#endif

        // TODO: Currently, the actual work stealing code sits in (big) Unity's native code w/ some dependencies
        // This is implemented trying to use the same code pattern:
        //        while (true)
        //        {
        //            if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out int begin, out int end))
        //                break;
        public static bool GetWorkStealingRange(ref JobRanges ranges, int jobIndex, out int begin, out int end)
        {
            if (ranges.State == WorkStealingState.Done)
            {
                begin = 0;
                end = 0;
                return false;
            }

#if UNITY_SINGLETHREADED_JOBS
            {
#else
            if (ranges.runOnMainThread > 0) {
#endif

                // There's only one thread, and the IndicesPerPhase don't have much meaning.
                // Do everything in one block of work.
                begin = 0;
                end = ranges.ArrayLength;
                ranges.State = WorkStealingState.Done;
                return end > begin;
            }
#if !UNITY_SINGLETHREADED_JOBS

            // Divide the work equally.
            // TODO improve by accounting for the indices per phase.

            int nWorker = JobWorkerCount > 0 ? JobWorkerCount : 1;
            begin = jobIndex * ranges.ArrayLength / nWorker;
            end = (jobIndex + 1) * ranges.ArrayLength / nWorker;

            if (end > ranges.ArrayLength)
                end = ranges.ArrayLength;
            if (jobIndex == nWorker - 1)
                end = ranges.ArrayLength;

            ranges.State = WorkStealingState.Done;
            return end > begin;
#endif
        }

        // Used by code-gen, and nothing but code-gen should call it.
        static unsafe int CountFromDeferredData(void* deferredCountData)
        {
            // The initial count (which is what tiny only uses) is the `int` past the first `void*`.
            int count = *((int*) ((byte*) deferredCountData + sizeof(void*)));
            return count;
        }

        public static unsafe JobHandle ScheduleParallelFor(ref JobScheduleParameters parameters, int arrayLength, int innerloopBatchCount)
        {
            return ScheduleParallelForInternal(ref parameters, arrayLength, null, innerloopBatchCount);
        }

        public static unsafe JobHandle ScheduleParallelForDeferArraySize(ref JobScheduleParameters parameters,
            int innerloopBatchCount, void* getInternalListDataPtrUnchecked, void* atomicSafetyHandlePtr)
        {
            return ScheduleParallelForInternal(ref parameters, -1, getInternalListDataPtrUnchecked, innerloopBatchCount);
        }

        static unsafe void CopyMetaDataToJobData(ref JobMetaData jobMetaData, void* managedJobDataPtr, void* unmanagedJobData)
        {
            jobMetaData.managedPtr = managedJobDataPtr;
            jobMetaData.unmanagedPtr = unmanagedJobData;
            if (unmanagedJobData != null)
                UnsafeUtility.CopyStructureToPtr(ref jobMetaData, unmanagedJobData);
            if (managedJobDataPtr != null)
                UnsafeUtility.CopyStructureToPtr(ref jobMetaData, managedJobDataPtr);
        }

        static unsafe void* AllocateJobHeapMemory(int jobSize, int n)
        {
            if (jobSize < 8) jobSize = 8;   // handles the odd case of empty job
            int metadataSize = UnsafeUtility.SizeOf<JobMetaData>();
            int allocSize = metadataSize + jobSize * n;
            void* mem = UnsafeUtility.Malloc(allocSize, 16, Allocator.TempJob);
            UnsafeUtility.MemClear(mem, allocSize);
            return mem;
        }

        static unsafe JobHandle ScheduleParallelForInternal(ref JobScheduleParameters parameters, int arrayLength, void* deferredDataPtr, int innerloopBatchCount)
        {
            // May provide an arrayLength (>=0) OR a deferredDataPtr, but both is senseless.
            Assert.IsTrue((arrayLength >= 0 && deferredDataPtr == null) || (arrayLength < 0 && deferredDataPtr != null));

            UnsafeUtility.AssertHeap(parameters.JobDataPtr.ToPointer());
            UnsafeUtility.AssertHeap(parameters.ReflectionData.ToPointer());
            ReflectionDataProxy jobReflectionData = UnsafeUtility.AsRef<ReflectionDataProxy>(parameters.ReflectionData.ToPointer());

            Assert.IsFalse(jobReflectionData.ExecuteFunctionPtr.ToPointer() == null);
            Assert.IsFalse(jobReflectionData.CleanupFunctionPtr.ToPointer() == null);
#if ENABLE_UNITY_COLLECTIONS_CHECKS && !UNITY_DOTSPLAYER_IL2CPP
            Assert.IsTrue((jobReflectionData.UnmanagedSize != -1 && jobReflectionData.MarshalToBurstFunctionPtr != IntPtr.Zero)
                || (jobReflectionData.UnmanagedSize == -1 && jobReflectionData.MarshalToBurstFunctionPtr == IntPtr.Zero));
#endif
            void* managedJobDataPtr = parameters.JobDataPtr.ToPointer();
            JobMetaData jobMetaData;

            UnsafeUtility.CopyPtrToStructure(parameters.JobDataPtr.ToPointer(), out jobMetaData);
            Assert.IsTrue(jobMetaData.jobDataSize > 0); // set by JobScheduleParameters
            Assert.IsTrue(sizeof(JobRanges) <= JobMetaData.kJobMetaDataIsParallelOffset);
            jobMetaData.JobRanges.ArrayLength = (arrayLength >= 0) ? arrayLength : 0;
            jobMetaData.JobRanges.IndicesPerPhase = (arrayLength >= 0) ? GetDefaultIndicesPerPhase(arrayLength) : 1; // TODO indicesPerPhase isn't actually used, except as a flag.
            jobMetaData.JobRanges.runOnMainThread = parameters.ScheduleMode == ScheduleMode.RunOnMainThread ? 1 : 0;
            jobMetaData.isParallelFor = 1;
            jobMetaData.deferredDataPtr = deferredDataPtr;

#if UNITY_SINGLETHREADED_JOBS
            bool runSingleThreadSynchronous = true;
#else
            bool runSingleThreadSynchronous = parameters.ScheduleMode == ScheduleMode.RunOnMainThread;
#endif
            JobHandle jobHandle = default;

            if (runSingleThreadSynchronous)
            {
                parameters.Dependency.Complete();
                UnsafeUtility.SetInJob(1);
                try
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS && !UNITY_DOTSPLAYER_IL2CPP

                    // If the job was bursted, and the job structure contained non-blittable fields, the UnmanagedSize will
                    // be something other than -1 meaning we need to marshal the managed representation before calling the ExecuteFn
                    if (jobReflectionData.UnmanagedSize != -1)
                    {
                        void* unmanagedJobData = AllocateJobHeapMemory(jobReflectionData.UnmanagedSize, 1);

                        void* dst = (byte*) unmanagedJobData + sizeof(JobMetaData);
                        void* src = (byte*) managedJobDataPtr + sizeof(JobMetaData);
                        UnsafeUtility.CallFunctionPtr_pp(jobReflectionData.MarshalToBurstFunctionPtr.ToPointer(), dst, src);

                        CopyMetaDataToJobData(ref jobMetaData, managedJobDataPtr, unmanagedJobData);

                        // In the single threaded case, this is synchronous execution.
                        UnsafeUtility.CallFunctionPtr_pi(jobReflectionData.ExecuteFunctionPtr.ToPointer(), unmanagedJobData, 0);
                        UnsafeUtility.CallFunctionPtr_p(jobReflectionData.CleanupFunctionPtr.ToPointer(), unmanagedJobData);
                    }
                    else
#endif
                    {
                        CopyMetaDataToJobData(ref jobMetaData, managedJobDataPtr, null);

                        // In the single threaded case, this is synchronous execution.
                        UnsafeUtility.CallFunctionPtr_pi(jobReflectionData.ExecuteFunctionPtr.ToPointer(), managedJobDataPtr, 0);
                        UnsafeUtility.CallFunctionPtr_p(jobReflectionData.CleanupFunctionPtr.ToPointer(), managedJobDataPtr);
                    }

#if UNITY_SINGLETHREADED_JOBS

                    // This checks that the generated code was actually called; the last responsibility of
                    // the generated code is to clean up the memory. Unfortunately only works in single threaded mode,
                    Assert.IsTrue(UnsafeUtility.GetLastFreePtr() == managedJobDataPtr);
#endif
                }
                finally
                {
                    UnsafeUtility.SetInJob(0);
                }

                return jobHandle;
            }
#if !UNITY_SINGLETHREADED_JOBS
#if ENABLE_UNITY_COLLECTIONS_CHECKS && !UNITY_DOTSPLAYER_IL2CPP
            // If the job was bursted, and the job structure contained non-blittable fields, the UnmanagedSize will
            // be something other than -1 meaning we need to marshal the managed representation before calling the ExecuteFn
            if (jobReflectionData.UnmanagedSize != -1)
            {
                int nWorker = JobWorkerCount > 1 ? JobWorkerCount : 1;
                void* unmanagedJobData = AllocateJobHeapMemory(jobReflectionData.UnmanagedSize, nWorker);

                for (int i = 0; i < nWorker; i++)
                {
                    void* dst = (byte*)unmanagedJobData + sizeof(JobMetaData) + i * jobReflectionData.UnmanagedSize;
                    void* src = (byte*)managedJobDataPtr + sizeof(JobMetaData) + i * jobMetaData.jobDataSize;
                    UnsafeUtility.CallFunctionPtr_pp(jobReflectionData.MarshalToBurstFunctionPtr.ToPointer(), dst, src);
                }

                // Need to change the jobDataSize so the job will have the correct stride when finding
                // the correct jobData for a thread.
                JobMetaData unmanagedJobMetaData = jobMetaData;
                unmanagedJobMetaData.jobDataSize = jobReflectionData.UnmanagedSize;
                CopyMetaDataToJobData(ref unmanagedJobMetaData, managedJobDataPtr, unmanagedJobData);

                jobHandle = ScheduleJobParallelFor(jobReflectionData.ExecuteFunctionPtr,
                    jobReflectionData.CleanupFunctionPtr, new IntPtr(unmanagedJobData), arrayLength,
                    innerloopBatchCount, parameters.Dependency);
            }
            else
#endif
            {
                CopyMetaDataToJobData(ref jobMetaData, managedJobDataPtr, null);
                jobHandle = ScheduleJobParallelFor(jobReflectionData.ExecuteFunctionPtr,
                    jobReflectionData.CleanupFunctionPtr, parameters.JobDataPtr, arrayLength,
                    innerloopBatchCount, parameters.Dependency);
            }

            if (parameters.ScheduleMode == ScheduleMode.Run)
            {
                jobHandle.Complete();
            }
#endif
            return jobHandle;
        }

        public static unsafe JobHandle Schedule(ref JobScheduleParameters parameters)
        {
            // Heap memory must be passed to schedule, so that Cleanup can free() it.
            UnsafeUtility.AssertHeap(parameters.JobDataPtr.ToPointer());
            UnsafeUtility.AssertHeap(parameters.ReflectionData.ToPointer());
            ReflectionDataProxy jobReflectionData = UnsafeUtility.AsRef<ReflectionDataProxy>(parameters.ReflectionData.ToPointer());

            Assert.IsTrue(jobReflectionData.ExecuteFunctionPtr.ToPointer() != null);
            Assert.IsTrue(jobReflectionData.CleanupFunctionPtr.ToPointer() != null);

#if ENABLE_UNITY_COLLECTIONS_CHECKS && !UNITY_DOTSPLAYER_IL2CPP
            Assert.IsTrue((jobReflectionData.UnmanagedSize != -1 && jobReflectionData.MarshalToBurstFunctionPtr != IntPtr.Zero)
                || (jobReflectionData.UnmanagedSize == -1 && jobReflectionData.MarshalToBurstFunctionPtr == IntPtr.Zero));
#endif

            void* managedJobDataPtr = parameters.JobDataPtr.ToPointer();
            JobMetaData jobMetaData;

            Assert.IsTrue(sizeof(JobRanges) <= JobMetaData.kJobMetaDataIsParallelOffset);
            UnsafeUtility.CopyPtrToStructure(managedJobDataPtr, out jobMetaData);
            Assert.IsTrue(jobMetaData.jobDataSize > 0); // set by JobScheduleParameters
            jobMetaData.managedPtr = managedJobDataPtr;
            UnsafeUtility.CopyStructureToPtr(ref jobMetaData, managedJobDataPtr);

#if UNITY_SINGLETHREADED_JOBS
            bool runSingleThreadSynchronous = true;
#else
            bool runSingleThreadSynchronous = parameters.ScheduleMode == ScheduleMode.RunOnMainThread || parameters.ScheduleMode == ScheduleMode.Run;
#endif
            JobHandle jobHandle = default;

            if (runSingleThreadSynchronous)
            {
                parameters.Dependency.Complete();
                UnsafeUtility.SetInJob(1);
                try
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS && !UNITY_DOTSPLAYER_IL2CPP

                    // If the job was bursted, and the job structure contained non-blittable fields, the UnmanagedSize will
                    // be something other than -1 meaning we need to marshal the managed representation before calling the ExecuteFn
                    if (jobReflectionData.UnmanagedSize != -1)
                    {
                        void* unmanagedJobData = AllocateJobHeapMemory(jobReflectionData.UnmanagedSize, 1);

                        void* dst = (byte*) unmanagedJobData + sizeof(JobMetaData);
                        void* src = (byte*) managedJobDataPtr + sizeof(JobMetaData);
                        UnsafeUtility.CallFunctionPtr_pp(jobReflectionData.MarshalToBurstFunctionPtr.ToPointer(), dst, src);

                        // In the single threaded case, this is synchronous execution.
                        // The cleanup *is* bursted, so pass in the unmanangedJobDataPtr
                        CopyMetaDataToJobData(ref jobMetaData, managedJobDataPtr, unmanagedJobData);
                        UnsafeUtility.CallFunctionPtr_pi(jobReflectionData.ExecuteFunctionPtr.ToPointer(), unmanagedJobData, 0);

#if UNITY_SINGLETHREADED_JOBS

                        // This checks that the generated code was actually called; the last responsibility of
                        // the generated code is to clean up the memory. Unfortunately only works in single threaded mode,
                        Assert.IsTrue(UnsafeUtility.GetLastFreePtr() == managedJobDataPtr);
#endif
                    }
                    else
#endif
                    {
                        CopyMetaDataToJobData(ref jobMetaData, managedJobDataPtr, null);

                        // In the single threaded case, this is synchronous execution.
                        UnsafeUtility.CallFunctionPtr_pi(jobReflectionData.ExecuteFunctionPtr.ToPointer(), managedJobDataPtr, 0);

#if UNITY_SINGLETHREADED_JOBS

                        // This checks that the generated code was actually called; the last responsibility of
                        // the generated code is to clean up the memory. Unfortunately only works in single threaded mode,
                        Assert.IsTrue(UnsafeUtility.GetLastFreePtr() == managedJobDataPtr);
#endif
                    }
                }
                finally
                {
                    UnsafeUtility.SetInJob(0);
                }

                return jobHandle;
            }
#if !UNITY_SINGLETHREADED_JOBS
#if ENABLE_UNITY_COLLECTIONS_CHECKS && !UNITY_DOTSPLAYER_IL2CPP
            // If the job was bursted, and the job structure contained non-blittable fields, the UnmanagedSize will
            // be something other than -1 meaning we need to marshal the managed representation before calling the ExecuteFn.
            // This time though, we have a whole bunch of jobs that need to be processed.
            if (jobReflectionData.UnmanagedSize != -1)
            {
                void* unmanagedJobData = AllocateJobHeapMemory(jobReflectionData.UnmanagedSize, 1);

                void* dst = (byte*)unmanagedJobData + sizeof(JobMetaData);
                void* src = (byte*)managedJobDataPtr + sizeof(JobMetaData);
                UnsafeUtility.CallFunctionPtr_pp(jobReflectionData.MarshalToBurstFunctionPtr.ToPointer(), dst, src);

                CopyMetaDataToJobData(ref jobMetaData, managedJobDataPtr, unmanagedJobData);
                jobHandle = ScheduleJob(jobReflectionData.ExecuteFunctionPtr, new IntPtr(unmanagedJobData), parameters.Dependency);
            }
            else
#endif
            {
                CopyMetaDataToJobData(ref jobMetaData, managedJobDataPtr, null);
                jobHandle = ScheduleJob(jobReflectionData.ExecuteFunctionPtr, parameters.JobDataPtr, parameters.Dependency);
            }
#endif
            return jobHandle;
        }

        public struct MinMax
        {
            public int Min;
            public int Max;
        }

        public static unsafe MinMax PatchBufferMinMaxRanges(IntPtr bufferRangePatchData, void* jobdata, int startIndex, int rangeSize)
        {
            return new MinMax
            {
                Min = startIndex,
                Max = startIndex + rangeSize - 1
            };
        }
    }

    public static class JobHandleUnsafeUtility
    {
        public static unsafe JobHandle CombineDependencies(JobHandle* jobs, int count)
        {
#if UNITY_SINGLETHREADED_JOBS
            return default(JobHandle);
#else
            var fence = new JobHandle();
            JobsUtility.ScheduleMultiDependencyJob(ref fence, JobsUtility.BatchScheduler, new IntPtr(jobs), count);
            return fence;
#endif
        }
    }
}

