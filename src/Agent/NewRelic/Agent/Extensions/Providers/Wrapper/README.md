# Creating and Maintaining Wrappers #

Wrappers are created in this isolated way because it gives us the most flexibility with external references used by the wrapped code. For a specific example of the need for this isolation see WCF3 & WCF4.  Both modules deal with WCF but each framework uses a well defined set of referenced assemblies that are versioned to the specific version of WCF - System.ServiceModel is a great example of this.

Unless there is a good, concrete reason to group logical wrappers together we should always consider keeping them separate to prevent version collision. While this method creates many projects (and compiled assemblies) it also helps ensure that we protect ourselves and our customers from having to deal with instrumentation that breaks on use.

### Setup & Configuration ###

1. Open the **FullAgent** solution

1. Create an empty **Class Library** project
	* Name the project after the framework being instrumented
	* Save the project to **[SOURCE DIR]\dotnet_agent\Agent\NewRelic\Agent\Extensions\Providers\Wrapper**

1. Set assembly name and default namespace
    > _Right-click project_ -> **Properties**
    >
    >> **Application**
	>> * Set **Assembly name** and **Default namespace** to 
	>> ```xml
	>> NewRelic.Providers.Wrapper.<your_project_name>

1. Update assembly info
	* Replace contents inside **AssemblyInfo.cs** with the following:
```cs
using System.Reflection;

[assembly: AssemblyTitle("<your_assembly_name>")]
[assembly: AssemblyDescription("<name_of_framework_being_instrumented> Wrapper Provider for New Relic .NET Agent")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("New Relic")]
[assembly: AssemblyProduct("New Relic .NET Agent")]
[assembly: AssemblyCopyright("Copyright © 2019")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
```
1. Sign the assembly
    > _Right-click project_ -> **Properties**
    >
    >> **Signing**
	>> * Check the **Sign the assembly** checkbox
	>>
	>>
	>> * Under **Choose a strong name key file**, select a key file by browsing to **KeyFile.snk** in any of the other wrapper projects

1. Configure the project so that its **Release** and **Debug** build configurations each target the **x64** and **x86** platforms
    > **Build** -> **Configuration Manager**
    >
    >> **Active Solution Configuration**
    >> *  Ensure that **Debug** and **Release** are the *only* configurations listed
    >>
    >> **Active Solution Platforms**
    >> *  Ensure that **x64** and **x86** are the *only* platforms listed
    >>
    >> **Project Contexts**
    >> * Ensure that the **Build** checkbox next to your project is checked
    >>  	 
	>> 	 _**NOTE:** You will have to do this a total of 4 times, once for each combination of configuration and platform selected from the 2 drop-down menus_

1. Add the following NuGet packages to your project. It is strongly recommended that you use the NuGet Package Manager Console accessible Tools -> NuGet Package Manager -> Package Manager Console, and that you specify the exact version of each of these assemblies.
	
	* **NewRelic.Agent.Extensions**
    * **NewRelic.Core** (Not Required)

_**NOTE:** Make sure to add the same version of the library that all other projects in the solution use_ 
    
	
4. Add your wrapper project as a dependent project of the **New Relic Home Builder** project which exists within the Installer folder. You do this as follows:
	* Right-click on the Solution in the Solution Explorer and select **Project Dependencies**
	* In the dialog that pops up, on the **Dependencies** tab, select **New Relic Home Builder** from the Projects drop-down list
	* Scroll down until you see your new wrapper, and check that box, then click the Ok button

### Creating the Wrapper ###

1. Add instrumentation file
	* Add a new file to the project called `Instrumentation.xml`
	* Set it to always copy to the output directory
		> **Solution Explorer**
		> * Select **Instrumentation.xml**
		>> **Properties**
		>> * Choose **Copy always** from the **Copy to Output Directory** drop-down list
	* Copy and paste the following contents:
```xml
<?xml version="1.0" encoding="utf-8" ?>
<extension xmlns="urn:newrelic-extension">
	<instrumentation>
		<tracerFactory>
			<match assemblyName="{name_of_framework_assembly}" className="{name_of_framework_assembly}.{name_of_class}">
				<exactMethodMatcher methodName="{name_of_method_being_wrapped}" />
			</match>
		</tracerFactory>
	</instrumentation>
</extension>
```

2. Add a Wrapper class for the method specified in the instrumentation file
	* Add a new file to the project called `<framework_method>Wrapper.cs`
	* Copy and paste the following contents:
```cs
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.WRAPPERNAMESPACE
{
	public class METHODNAMEWrapper: IWrapper
	{
		private const string TypeName = "CLASSNAME";
		private const string MethodName = "METHODNAME";

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: "ASSEMBLYNAME", typeName: TypeName, methodName: MethodName);
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi)
		{
			var segment = agentWrapperApi.StartMethodSegment(instrumentedMethodCall.MethodCall, instrumentedMethodCall.MethodCall.Method.Type.ToString(), instrumentedMethodCall.MethodCall.Method.MethodName);
			return Delegates.GetDelegateFor(segment);
		}
	}
}
```
> _**NOTE:**_ 
> 
> A Wrapper’s `IWrapper.CanWrap` method is only called the first time a wrapped method is called. `IWrapper.BeforeWrappedMethod` method will be called every time a handled method is called.
>
>  `CanWrap` should return true if the assembly name, type/class name, name of the method that’s being called (and sometimes assembly version, if your wrapper only works on specific versions of the target framework) match what the wrapper says it can handle.  `BeforeWrappedMethod` should do any "beginning" work associated with the method call, such as starting transactions or segments. `BeforeWrappedMethod` should return a delegate that will perform any necessary "ending" cleanup work. The factory can also return `Delegates.NoOp` if no cleanup work is necessary.

### Registering the Wrapper with the Agent Installer ###
##### _Note: Functional tests use the agent installer to install the agent being tested_ #####

1. Open **Product.wxs** from the WIX project called **Installer** inside the **FullAgent** solution

2. Add a new component for your wrapper assembly
	* Copy and paste the following code below the `<!-- Wrappers -->` comment:
	
```xml
			<Component Id="{name_of_your_wrapper}WrapperComponent" Guid="">
				<File Id="{name_of_your_wrapper}WrapperFile" Name="NewRelic.Providers.Wrapper.{name_of_your_wrapper}.dll" KeyPath="yes" Source="$(var.SolutionDir)New Relic Home $(var.Platform)\Extensions\NewRelic.Providers.Wrapper.{name_of_your_wrapper}.dll"/>
			</Component>
```

3. Add a new component for your wrapper instrumentation file
	* Copy and paste the following code below the `<!-- Wrapper Instrumentation Files-->` comment:

```xml
			<Component Id="{name_of_your_wrapper}InstrumentationComponent" Guid="">
				<File Id="{name_of_your_wrapper}InstrumentationFile" Name="NewRelic.Providers.Wrapper.{name_of_your_wrapper}.Instrumentation.xml" KeyPath="yes" Source="$(var.SolutionDir)New Relic Home $(var.Platform)\Extensions\NewRelic.Providers.Wrapper.{name_of_your_wrapper}.Instrumentation.xml"/>
			</Component>
```

4.  Generate a GUID for each new component

	> **Tools -> Create Guid**
	>> * Choose the **Registry Format** option
	>> 
	>> * Copy the result by clicking **Copy**
	>> 
	>> * Paste the new GUID inside the empty quotes of the Guid property of one of your Components
	>> 
	>> * Generate a different GUID using the same method for the other Component

### Handling exceptions ###

There are a number of different ways for the agent to notice or handle exceptions, and each one has a specific use case.

#### Exceptions that occur in the user's application ####

Use `AgentWrapperApi.NoticeError` to notice errors in the user's application. This will result in the error being attached to the transaction where it will be eligible to turn into an error trace and/or an error event, and the transaction will be marked as an error and will get an "F" apdex score.

The agent should not be too aggressive in noticing user application exceptions. Generally the agent should only notice exceptions in "top-level" instrumentation such the ASP pipeline or a custom transaction, or in specific error-handling framework methods. Otherwise, the agent will notice exceptions even if those exceptions are later caught in a try/catch in the user's code. This would result in the agent calling the transaction an error even if the user code recovers successfully from the exception.

**No errors that originate in the wrapper should be noticed by this method.** Why? Because if this method is used to notice wrapper errors, then an error that occurs in the wrapper will result in the user's transaction being marked as an error, which is not correct. In the worst case, it could trigger false alerts.

#### Exceptions that occur in the wrapper ####

Most of the time you should just let the exception bubble out of the wrapper, where it will be caught and logged by the agent's last-chance exception handler. Note that repeated consecutive exceptions bubbling of the wrapper will result in the instrumentation for that method being removed (i.e. the agent will start ignoring that instrumentation until the agent is restarted). In many cases this is ideal behavior; for example, if a wrapper is released with a bug that causes it to _always_ fail, it is ideal for the agent to disable instrumentation handled by that wrapper to eliminate the performance penalty of exceptions being repeated thrown and caught.

You should use `AgentWrapperApi.HandleWrapperException` to deal with exceptions in a wrapper that **can't** or otherwise **shouldn't** be handled by bubbling the exception up.

* When can't an exception be handled by bubbling? The most common example is when a wrapper attaches a continuation to a task -- that continuation will not execute in the context of the agent call stack, and thus will not be covered by the agent's last-chance exception handler.
* When shouldn't an exception be handled by bubbling? This is subjective, but the vague answer is "any time you don't want the agent to decide to turn off instrumentation". For example, if an exception seems like it will be intermittent, or if the exception is non-critical to the functionality of the wrapper, then you probably want to use `AgentWrapperApi.HandleWrapperException` so that we can notice and log the problem without risking the instrumentation being disabled by the agent. 

## Best Practices ##

* Do not use any third party libraries. **NewRelic.Agent.Extensions** and **NewRelic.Core** can be used.

* Do not statically reference the framework that your wrapper is targeting.
	
	> The wrappers are loaded at agent startup, which may be before its target framework has been loaded. Statically referencing the target framework will cause the wrapper to fail to load because it won't be able to find a reference to the target framework.
	>
	> If you must have static framework references then nest them inside an inner static class. See `StackExchangeRedis.Statics` for an example.


* Do not return null from `BeforeWrappedMethod`. Instead, return `Delegates.NoOp`, or in case of truly exceptional conditions, throw an exception.


* Use `Delegates.GetDelegateFor` if the finishing work your wrapper does is simple. For example, `return Delegates.GetDelegateFor(() => api.EndSegment(segment))`.


* Do not add a non-default constructor to your `IWrapper`.


## Notes on dynamic wrapper assembly loading ##

Wrapper assemblies are loaded dynamically at agent startup. All assemblies found in **[NEWRELIC_HOME]/Extensions** will be loaded, and all `IWrapper`s found in the assembly will be instantiated and passed to the `WrapperService` (contained inside a `LazyMap`).

When an instrumented method is hit, `WrapperService` will ask the `LazyMap` for an appropriate `IWrapper`. The map will return the first wrapper it finds that returns **true** from `IWrapper.CanWrap`, or null if they all return **false**. The resulting wrapper (or null) will be cached in the map which is keyed on the fully qualified method signature. If the wrapper loader returns null, the agent will fall back to using tracers.

To deal with version conflicts, `AssemblyResolutionService` registers an assembly resolution failure event handler (`AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolutionFailure;`). When an assembly fails to load as a result of a wrapper factory call, this handler will attempt find any loaded assemblies which match the name of the assembly that failed to load. For example, if a `Wrapper` is compiled against NServiceBus 3.0.0, but at run time NServiceBus 3.0.1 is loaded into memory, an assembly resolution failure will occur, but this handler will be able to dynamically redirect to the 3.0.1 assembly.
