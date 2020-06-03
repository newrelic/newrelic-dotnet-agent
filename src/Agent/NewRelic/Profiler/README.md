NewRelic.Profiler
====

For coding standards information, please see: [Agent Coding Standards](https://source.datanerd.us/dotNetAgent/dotnet_agent/wiki/Agent-Coding-Standards)

# Building the Profiler

## Prerequisites

* Visual Studio 2017
	* Select these two 'Workloads' during installation (or modify your installation):
		* Desktop Development with C++.
		* Universal Windows Platform development (this is optional but highly recommended for future development)
* Install the Windows 8.1 SDK separately (i.e. NOT through Visual Studio).  Download from here: https://developer.microsoft.com/en-us/windows/downloads/windows-8-1-sdk

## Building on Windows
* The profiler solution can be built within Visual Studio 2017 using the standard solution build menu option. This will also build unit tests and the Nuget Package Generator executable, which is used to generate a Nuget package. That package is pushed to our [internal Nuget site](http://win-nuget-repository.pdx.vm.datanerd.us:81/) by the [Profiler Jenkins job](https://dotnet-build.pdx.vm.datanerd.us/view/All/job/DotNet-NuGet-NewRelic.Profiler/).

## Building on MacOS

1. Checkout the coreclr project (https://github.com/dotnet/coreclr) and follow the directions to build it
1. Run `CORECLR_NEWRELIC_HOME=~/coreclr docker-compose run build` with the proper coreclr path
1. `libCorProfiler.dylib` should be built into the root directory

## Running on MacOS

1. Stage a home directory containing the agent binaries for coreclr.  In this example that is defined as `~/nrcorehome`
1. Create a test core 2.0 project and build it
1. Instrument some methods in the app with custom xml instrumentation
1. Run the app with the profiler attached

    `NEWRELIC_INSTALL_PATH=~/newrelic/NewRelic.Profiler/Test/NewRelic.Agent.Core/bin/Debug/netcoreapp2.0/  NEWRELIC_HOME=~/nrcorehome CORECLR_PROFILER={71DA0A04-7777-4EC6-9643-7D28B46A8A41} CORECLR_ENABLE_PROFILING=1 CORECLR_PROFILER_PATH=~/newrelic/NewRelic.Profiler/libCorProfiler.dylib  dotnet run`

## Docker on Windows

* After installing docker, go into the settings and make sure the C drive is shared.
* If you use `git bash` as a shell for running docker commands to be able to attach to and interact with Docker containers you will need to do one of the following:
	* During initial Git Bash setup, choose "Use Windows' default console window" instead of "Use MinTTY"
	* Always prepend "winpty" to any Docker commands which will require STDIN/OUT to be attached to a terminal (e.g. "winpty docker attach $containerId")
* Running docker in PowerShell or cmd.exe work fine.

## Building the Linux profiler

The Linux profiler build is docker based.

    docker-compose build build
    docker-compose run build

Building the `build` service does the following:

1. Build a container based on ubuntu 14.04
1. Check out the `coreclr` repo
1. Configure tooling to build the profiler

Running the `build` service runs `build_profiler.sh` inside of the container.

The script builds the `libNewRelicProfiler.so` binary into the root profiler directory.

## Testing the Linux profiler

1. Make sure `CORECLR_NEWRELIC_HOME` points to a valid coreclr agent home directory (A `New Relic Agent x64 CoreCLR` build from the managed agent).
1. Run `docker-compose build test && docker-compose run test` from the root directory to run a docker container with CoreCLR 2.0 installed with a sample mvc app.

If you want to play around in the test container run `docker-compose run test bash`.

The test container's environment is set up to run applications with instrumentation.  The `profiler` directory is mapped to the profiler repo on the host machine and the `agent` directory is mapped to `CORECLR_NEWRELIC_HOME` on the host.

IMPORTANT - the profiler requires libc++.  If the profiler isn't attaching, use `ldd` on the profiler shared library and verify that its dependencies are resolved.

`sudo apt-get install -y libc++1`

# What the Project Does

## How the Profiler Attaches To a Process

These steps are all executed by the CLR as defined by the Microsoft profiling spec.

1. If `COR_ENABLE_PROFILING` environment variable is missing or set to something other than 1 then no profiler is attached.
1. Get the GUID of the profiler from `COR_PROFILER` environment variable.
1. Find path to profiler DLL.
 1. If running CLR version 4 or higher **AND** `COR_PROFILER_PATH` environment variable is set then use that
 1. Else lookup the GUID found in `COR_PROFILER` in the registry under `HKEY_CLASSES_ROOT\CLSID`.
1. Load the profiler DLL (`NewRelic.Profiler.dll`) off disk and into memory.
1. Read the profiler DLL as a COM library.
1. Instantiate the registered `ICorProfilerCallback`.
1. Call `ICorProfilerCallback.Initialize`.
 1. If `Initialize` returns anything other than `S_OK` detach the profiler (in CLR4 the DLL unloads, in CLR2 it stays in memory unused)

## How the Profiler Receives Notifications

When the `ICorProfilerCallback.Initialize` is called, the profiler has an opportunity to set a number of flags indicating the types of events it wants to monitor.  The most interesting one is `COR_PRF_MONITOR_JIT_COMPILATION` which will result in `ICorProfilerCallback.JITCompilationStarted` being called every time a method is JIT compiled.  In CLR2, JIT compilation happens on the first call to any given method.  In CLR4, the same is true but a method can be flagged for ReJIT which means it will be JIT compiled again on its next execution after ReJIT was flagged.  We currently don't support ReJIT (it is unclear what effects ReJITting may have on our injected code).

## How the Profiler Injects Code

When `ICorProfilerCallback.JITCompilationStarted` is called, the profiler has an opportunity to change the about-to-be-JIT-compiled-method's byte code.  We first lookup the method to see if we want to inject into it (in most cases we don't and bail out as soon as we've determined that to reduce overhead).  Once we have identified the method as 'interesting enough to be instrumented' we ask for a reJIT because if we modify the bytecode in this initial JIT event later calls to `Revert` won't work correctly.  When the reJIT starts the `ICorProfilerCallback.ReJITCompilationStarted` event will fire.  If it is for a function we want to instrument we grab the original bytecode that makes up the method's body wrap it with our own logic.  We then take the resulting bytecode and give it back to the CLR, telling the CLR that this new bytecode is
what should be JIT compiled (the old bytecode is no longer referenced/used).

## Keeping track of the original bytecode for reJIT

We fetch the bytecode for methods by calling `ICorProfilerInfo4.GetILFunctionBody`.  For any module/method id instance, the first call to this method will return the original bytecode.  After we modify the bytecode by calling `ICorProfilerInfo4.SetILFunctionBody`, subsequent calls to `GetILFunctionBody` will return our _modified_ bytecode.  Because of this we maintain a map that tracks the original bytecode for all of the methods that we modify with instrumentation.

## What we Inject

The exact ByteCode we inject can be found about [here](https://source.datanerd.us/dotNetAgent/NewRelic.Profiler/blob/master/MethodRewriter/FunctionManipulator.h#L487-L651).  Below I have written some pseudo-code that is more or less what we inject, though not exactly:
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

## Instrumentation Refreshes (using reJIT)

When instrumentation in the `extensions` changes on disk, the profiler will re-instrument the application to reflect the new instrumentation.  Here's how that works:

 * The managed agent watches the `extensions` directory.  When it notices a change it notifies the profiler by invoking `InstrumentationRefresh`.
 * The profiler creates a pointer to the previous instrumentation and then reads in the new instrumentation (including any "live" instrumentation we've received from the RPM service).
 * Both the old and new sets of instrumentation points are sorted by the assembly they reference.
 * The profiler iterates through all modules, looking up the assembly name for the module and then fetching the old and new instrumentation points for that assembly.
 * Given a set of instrumentation points for an assembly we find all of the class/methods that match the instrumentation.  For those matching old instrument, we request a `Revert`.  After that, we request a `ReJIT` for all of the methods matching the new instrumentation.

 This brute force approach to updating instrumentation means that we will revert and rejit methods for which instrumentation has not changed.  The advantage is it keeps the logic very simple, reducing the likelihood of bugs.  We don't instrument very many methods either, so the overhead of the rejits should be fairly low.

## Environment Variables

The Microsoft defined environment variables like `COR_ENABLE_PROFILING` and `COR_PROFILER_PATH` used to attach the profiler to a process.  On CoreCLR all of those environment variables are prefixed with `CORECLR` instead of `COR`, ie `CORECLR_ENABLE_PROFILING`.

After the profiler attaches it uses custom environment variables to determine the path to `NewRelic.Agent.Core.dll`.  It tries to find the dll in the path specified by `NEWRELIC_INSTALL_PATH`, or if that is undefined then `NEWRELIC_HOME`.  On CoreCLR, those environment variables are prefixed with `CORECLR_` so that the agent does not try to use the .NET Framework managed agent in a CoreCLR process.

# FAQ

## What's a good starting point to check out in the code?

The main entry point for the profiler is the `Initialize` method in [CorProfilerCallbackImpl.h](Profiler/CorProfilerCallbackImpl.h).  If you want to see what we do when we modify methods check out [InstrumentFunctionManipulator.h](MethodRewriter/InstrumentFunctionManipulator.h).

## Why did the profiler detach without providing meaningful feedback?

Unfortunately that might happen for many different reasons.  You'll see terse event log messages if an x86 profiler tries to attach to an x64 process (or vice versa).  If the home directory isn't found, the profiler will detach without logging anything meaningful because it won't know the log path.

## What is with `System.CannotUnloadAppDomainException`?

We need a place to put some static methods that the injected code can call that are the least likely to have an impact on user code.  `System.CannotUnloadAppDomainException` is an exception that is almost never seen, has been around since .NET 2.0, is always available, and is unlikely to be the target of reflection.

Also, `AppDomain.GetData` and `SetData` are security critical and direct invocations to these methods cannot be injected into all classes.  We can safely call these methods from helper methods in `System.CannotUnloadAppDomainException` and then safely inject calls to those helper methods.

The bytecode we inject into CoreCLR applications does not reference `System.CannotUnloadAppDomainException`.

## Links

* ECMA-335, CIL specification: http://www.ecma-international.org/publications/standards/Ecma-335.htm
* CIL Instruction List: https://en.wikipedia.org/wiki/List_of_CIL_instructions
* Broman post on ReJIT.  Note that the approach he suggests for implementing rejitting did not work well for us.  https://blogs.msdn.microsoft.com/davbr/2011/10/12/rejit-a-how-to-guide/
