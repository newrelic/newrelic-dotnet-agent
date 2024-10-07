# NewRelic.Profiler

## Building the Profiler

Refer to our [development documentation](../../../../docs/development.md#profilersln).

## What the Project Does

### How the Profiler Attaches To a Process

These steps are all executed by the CLR as defined by the Microsoft profiling spec.

1. If `COR_ENABLE_PROFILING`/`CORECLR_ENABLE_PROFILING` environment variable is missing or set to something other than 1 then no profiler is attached.
1. If `COR_PROFILER`/`CORECLR_PROFILER` environment variable is missing or set to something other than a GUID (any) then no profiler is attached.
1. Find path to profiler DLL.
    1. If the `COR_PROFILER_PATH`/`CORECLR_PROFILER_PATH` environment variable is set then use that.
    1. Else lookup the GUID found in `COR_PROFILER` in the registry under `HKEY_CLASSES_ROOT\CLSID` (.NET Framework only).
        - .NET Framework New Relic profiler GUID: `{71DA0A04-7777-4EC6-9643-7D28B46A8A41}`
1. Load the profiler DLL (`NewRelic.Profiler.dll`) off disk and into memory.
1. Read the profiler DLL as a COM library.
1. Instantiate the provided `CorProfilerCallbackImpl`.
1. Call `CorProfilerCallbackImpl.Initialize`.
    - If `Initialize` returns anything other than `S_OK` detach the profiler.

### How the Profiler Receives Notifications

When the `CorProfilerCallbackImpl.Initialize` is called, the profiler has an opportunity to set a [number of flags](https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/cor-prf-monitor-enumeration) indicating the types of events it wants to monitor.  The most interesting one is `COR_PRF_MONITOR_JIT_COMPILATION` which will result in `CorProfilerCallbackImpl.JITCompilationStarted` being called every time a method is JIT compiled.

Other flags the profiler sets:
* COR_PRF_MONITOR_JIT_COMPILATION
* COR_PRF_MONITOR_MODULE_LOADS
* COR_PRF_USE_PROFILE_IMAGES
* COR_PRF_MONITOR_THREADS
* COR_PRF_ENABLE_STACK_SNAPSHOT
* COR_PRF_ENABLE_REJIT
* COR_PRF_DISABLE_ALL_NGEN_IMAGES
* COR_PRF_HIGH_DISABLE_TIERED_COMPILATION

### How the Profiler Injects Code

When `CorProfilerCallbackImpl.JITCompilationStarted` is called, the profiler has an opportunity to change the about-to-be-JIT-compiled-method's byte code.  We first lookup the method to see if we want to inject into it (in most cases we don't and bail out as soon as we've determined that to reduce overhead).  Once we have identified the method as 'interesting enough to be instrumented' we ask for a reJIT because if we modify the bytecode in this initial JIT event later calls to `Revert` won't work correctly.  When the reJIT starts the `CorProfilerCallbackImpl.ReJITCompilationStarted` event will fire.  If it is for a function we want to instrument we grab the original bytecode that makes up the method's body wrap it with our own logic.  We then take the resulting bytecode and give it back to the CLR, telling the CLR that this new bytecode is
what should be JIT compiled (the old bytecode is no longer referenced/used).

### Keeping track of the original bytecode for ReJIT

We fetch the bytecode for methods by calling `ICorProfilerInfo4.GetILFunctionBody`.  For any module/method id instance, the first call to this method will return the original bytecode.  After we modify the bytecode by calling `ICorProfilerInfo4.SetILFunctionBody`, subsequent calls to `GetILFunctionBody` will return our _modified_ bytecode.  Because of this we maintain a map that tracks the original bytecode for all of the methods that we modify with instrumentation.

### What we inject

The exact ByteCode we inject can be found about [here](MethodRewriter/FunctionManipulator.h).  Below I have written some pseudo-code that is more or less what we inject, though not exactly:
```cs
	try
	{
		try
		{
			// GetTracer is invoked reflectively by calling Assembly.LoadFrom(path), Type.GetMethod(..)
			finishTracerDelegate = AgentShim.GetFinishTracerDelegate(tracerFactoryName, tracerFactoryArgs, metricName, assemblyName, type, typeName, functionName, argumentSignatureString, this, /*<object[] of instrumented method's parameters>*/);
		} catch (Exception) {}

		// original method body here, with RET instruction changed to NOP
		result = // return value of original method or null for VOID
	}
	catch (Exception ex)
	{
		try
		{
			// the finish delegate is directly invoked as an `Action` delegate if the CLR > 2
			finishTracerDelegate(null, ex);
		} catch (Exception) {}
		rethrow;
	}

	try
	{
		finishTracerDelegate(result, null);
	} catch (Exception) {}

	return result;
```

Our CoreCLR instrumentation does not use the `System.CannotUnloadAppDomainException` helper methods.  Instead, it reflectively looks up
the agent core assembly and the `GetTracer` method for each tracer invocation.

### Instrumentation refreshes (using ReJIT)

When instrumentation in the `extensions` directory changes on disk, the profiler will re-instrument the application to reflect the new instrumentation.  Here's how that works:

 * The managed agent watches the `extensions` directory.  When it notices a change it notifies the profiler by invoking `InstrumentationRefresh`.
 * The profiler creates a pointer to the previous instrumentation and then reads in the new instrumentation (including any "live" instrumentation we've received from the RPM service).
 * Both the old and new sets of instrumentation points are sorted by the assembly they reference.
 * The profiler iterates through all modules, looking up the assembly name for the module and then fetching the old and new instrumentation points for that assembly.
 * Given a set of instrumentation points for an assembly we find all of the class/methods that match the instrumentation.  For those matching old instrument, we request a `Revert`.  After that, we request a `ReJIT` for all of the methods matching the new instrumentation.

 This brute force approach to updating instrumentation means that we will revert and rejit methods for which instrumentation has not changed.  The advantage is it keeps the logic very simple, reducing the likelihood of bugs.  We don't instrument very many methods either, so the overhead of the rejits should be fairly low.

### Environment Variables

The Microsoft defined environment variables like `COR_ENABLE_PROFILING` and `COR_PROFILER_PATH` used to attach the profiler to a process.  On CoreCLR all of those environment variables are prefixed with `CORECLR` instead of `COR`, ie `CORECLR_ENABLE_PROFILING`.

After the profiler attaches it uses custom environment variables to determine the path to `NewRelic.Agent.Core.dll`.  It tries to find the dll in the path specified by `NEW_RELIC_INSTALL_PATH`, or if that is undefined then `NEW_RELIC_HOME`.  On CoreCLR, those environment variables are prefixed with `CORECLR_` so that the agent does not try to use the .NET Framework managed agent in a CoreCLR process.

## FAQ

### What's a good starting point to check out in the code?

The main entry point for the profiler is the `Initialize` method in [CorProfilerCallbackImpl.h](Profiler/CorProfilerCallbackImpl.h).  If you want to see what we do when we modify methods check out [InstrumentFunctionManipulator.h](MethodRewriter/InstrumentFunctionManipulator.h).

### Why did the profiler detach without providing meaningful feedback?

Unfortunately that might happen for many different reasons.  You'll see terse event log messages if an x86 profiler tries to attach to an x64 process (or vice versa).  If the home directory isn't found, the profiler will detach without logging anything meaningful because it won't know the log path.

### What is with `System.CannotUnloadAppDomainException`?

We need a place to put some static methods that the injected code can call that are the least likely to have an impact on user code.  `System.CannotUnloadAppDomainException` is an exception that is almost never seen, has been around since .NET 2.0, is always available, and is unlikely to be the target of reflection.

Also, `AppDomain.GetData` and `SetData` are security critical and direct invocations to these methods cannot be injected into all classes.  We can safely call these methods from helper methods in `System.CannotUnloadAppDomainException` and then safely inject calls to those helper methods.

The bytecode we inject into CoreCLR applications does not reference `System.CannotUnloadAppDomainException`.

### Links

* ECMA-335, CIL specification: https://www.ecma-international.org/publications-and-standards/standards/ecma-335/
* CIL Instruction List: https://en.wikipedia.org/wiki/List_of_CIL_instructions
* Broman post on ReJIT.  Note that the approach he suggests for implementing rejitting did not work well for us.  https://blogs.msdn.microsoft.com/davbr/2011/10/12/rejit-a-how-to-guide/
