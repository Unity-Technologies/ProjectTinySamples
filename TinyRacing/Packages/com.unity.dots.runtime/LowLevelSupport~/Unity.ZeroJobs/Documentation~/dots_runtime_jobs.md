# Jobs and Threads it the DOTS Runtime

ST is an abbreviation for "single threaded" while MT is "multi-threaded."

## Design notes

ST vs MT is a compile time switch. The JobWorkerCount (the number of threads in the job pool) is set at startup and is constant through the lifetime of the program. (We may change this in the future.)

* When compiled in ST, the nativejobs lib isn't used or compiled in, and there is no thread worker pool. All the job methods are called from function pointers. 
* When compiled in MT the nativejobs lib is included. Dependending on the job, the job pool in nativejobs may be used, or the code may fall back to function pointers. (So MT is a superset of the ST code and functionality.)

### Single-Threaded

ST runs jobs by calling function pointers to the job (for example the Execute method) on the main thread. There is marshalling from C# to C++, but execution is synchronous. Jobs are run at their `Schedule()`; if you stop in the debugger on the `Execute()`method, you can see the `Schedule()` up the stack.

Parallel jobs are still synchronous, and called as if there was one thread.

`Complete()` and `JobHandles` don't do anything since the job is run at the `Schedule()` call.

### Multi-Threaded

In multi-threaded mode, the nativejobs library is used, which is a worker pool with job dependency management.

`Schedule()` places the job into the worker pool; it may run before the `Complete()` but isn't guaranteed to do so.

There are more job modes than one might expect. These reflect the possible set of inputs passed via the `JobScheduleParameters` parametr to the `ScheduleParallelForInternal()` and `Schedule()` methods in Unity.Jobs.LowLevel.cs, not interfaces to actual Job Producers. There probably isn't any reason for a Job to support all these modes - these are documented as the modes a Job Producer can use.

* Batched Single jobs ("ScheduleSingle") are scheduled on one worker thread to run asynchronously.
* Batched Parallel ("ScheduleParallel") jobs are scheduled on all worker threads to run asynchronously. Currently in DOTS-Runtime, Parallel jobs are split into JobWorkerCount threads, no more and no less. (This simplifies code and debugging. We may change this in the future.)
* Run Single jobs ("Run") are run synchronously on the main thread. Dependencies are Complete()ed before the job runs. An empty JobHandle is returned (if at all.)
* Run Parallel jobs (no obvious name - odd case) are run on JobWorkerCount worker threads. Dependencies are Completed()ed before the job runs. The job system will wait for completion of all workers before returning. An empty JobHandle is returned (if at all.)
* Jobs which are not HPC# safe are run synchronously on the main thread, whether Single or Parallel. This essentially drops back to single threaded mode for these jobs.

#### Deferred Array Size (aka deferred ranges, deferredCountData)

`deferredCountData` - where a range is passed from one job to another - is fully supported in ST and MT mode. Code-gen is used to insert the necessary code to fetch the range into the JobProducer's `Execute()` method. Any
job may use the `deferredCountData` by passing it to `ScheduleParallelForDeferArraySize()` which is a helper wrapper to `ScheduleParallelForInternal()`.




