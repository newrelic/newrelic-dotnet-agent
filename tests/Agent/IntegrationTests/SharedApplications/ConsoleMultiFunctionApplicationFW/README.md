# Console Multifunction Application and Test Fixture
This is a console application that accepts input as a series of commands/exercies to run.  It is analagous to a controller actions in an MVC/WebAPI app.  


## Usage

#### ```Help```,  ```Usage```, or ```?``` command
Displays a list of registered libraries, their available methods and the parameters required for them.
```
$ dotnet run  --framework netcoreapp2.2

Console Multi Function Application
Process Info: dotnet 5880

10:00:00 AM >usage

10:00:02 AM :USAGE:
10:00:02 AM :
10:00:02 AM :Here's a list of registered Library Methods:
10:00:02 AM :
10:00:02 AM :   PERFORMANCEMETRICS
10:00:02 AM :
10:00:02 AM :           PerformanceMetrics Test {countGCCollect:Int32} {countMaxWorkerThreads:Int32} {countMaxCompletionThreads:Int32}
10:00:02 AM :
10:00:02 AM :   SAMPLELIBRARY
10:00:02 AM :
10:00:02 AM :           SampleLibrary SampleLibrarymethod {howManyTimes:Int32}
10:00:02 AM :
10:00:02 AM >
```

#### ```quit``` or ```exit``` command
Closes the application

#### Invoking a Library Method
Based onthe above usage, the following is a call to the ```PerformanceMetrics``` Library's ```Test``` Method with 3 parameters.
```
10:04:28 AM >PerformanceMetrics Test 4 282 123

10:04:53 AM :EXECUTING: 'PerformanceMetrics Test 4 282 123'
10:04:53 AM :Setting Threadpool Max Threads: 282 worker/123 completion.
10:04:53 AM :Instrumented Method to start the Agent
10:04:54 AM :Forcing a GC.Collect
10:04:55 AM :Forcing a GC.Collect
10:04:56 AM :Forcing a GC.Collect
10:04:57 AM :Forcing a GC.Collect
10:04:58 AM >
```


## Defining a Library
Defining a library involves adding the ```[Library]``` Attribute to a class and the ```[LibraryMethod]``` attribute to the methods that should be exposed.

In the example below, a ```PerformanceMetrics``` Library is being made available.  Within it, the ```Test``` method is available to be called.  The ```StartAgent``` method is not decorated, and is not available to be called using the multifunction app.

```csharp
[Library]
public static class PerformanceMetrics
{
	[LibraryMethod]
	public static void Test(int countGCCollect, int countMaxWorkerThreads, int countMaxCompletionThreads)
	{

		Log.Info($"Setting Threadpool Max Threads: {countMaxWorkerThreads} worker/{countMaxCompletionThreads} completion.");

		ThreadPool.SetMaxThreads(countMaxWorkerThreads, countMaxCompletionThreads);

		StartAgent();

		Exercise(countGCCollect);
	}

	/// <summary>
	/// This is an instrumented method that doesn't actually do anything.  Its purpose
	/// is to ensure that the agent starts up.  Without an instrumented method, the agent won't
	/// start.
	/// </summary>
	[Transaction]
	private static void StartAgent()
	{
		Log.Info("Instrumented Method to start the Agent");
		Thread.Sleep(TimeSpan.FromSeconds(1));		//Get everything started up.
	}

	private static void Exercise(int countGCCollect)
	{
		for (var i = 0; i < countGCCollect; i++)
		{
			Log.Info("Forcing a GC.Collect");
			GC.Collect();
			GC.WaitForFullGCComplete();
			Thread.Sleep(TimeSpan.FromSeconds(1));
		}
	}
}
```

#### Logging Facility
```Log.Info``` and ```Log.Error``` methods are avilable and should be used.  They add timestamps to the output which may be useful in testing.


#### Library Design Considerations
* If a Library Method is not static, the Library needs to have a parameterless constructor.
* Method overloading is possible, but not recommended.  The matching of a command to the class and method uses the # of parameters to match to a single method, but not the types.  If a single matching cannot is not possible, an error will be thrown.
* The library class needs to be within the ConsoleMultiFunctionApplication project.  Since reflection is used to identify class and method, external references will not be loaded if they are not directly referenced in code.  There are ways around this that can be implemented as needed.


## Integration Tests
The ```ConsoleDynamicMethodFixtureFW``` and ```ConsoleDynamicMethodFixtureCore``` fixtures are designed to call the ConsoleMultiFunctionApplication.

In the example below, an abstract base test fixture has been designed.  Specific Framework and Core implementations are implemented classes.  Since this example tests the same functions between NetCore and NetFramework, the test is in the absract.

```Csharp

public abstract class DotNetPerfMetricsTests<TFixture> : IClassFixture<TFixture> where TFixture:ConsoleDynamicMethodFixture
{

	public DotNetPerfMetricsTests(TFixture fixture, ITestOutputHelper output)
	{
		Fixture = fixture;
		Fixture.TestLogger = output;
		
		///////////////////////////////////////////////////////////////////////////////////////
		//Here is where you would add the commands you would like to invoke
		//You can add as many as you want.
		///////////////////////////////////////////////////////////////////////////////////////
		Fixture.AddCommand($"PerformanceMetrics Test {COUNT_GC_INDUCED} {THREADPOOL_WORKER_MAX} {THREADPOOL_COMPLETION_MAX}");

		Fixture.Actions
		(
			setupConfiguration: () =>
			{
				Fixture.RemoteApplication.NewRelicConfig.SetLogLevel("finest");
			}
		);

		Fixture.Initialize();
	}

	[Fact]
	public void Test1()
	{ ... }
}

public class DotNetPerfMetricsTestsCore : DotNetPerfMetricsTests<ConsoleDynamicMethodFixtureCore>
{}

public class DotNetPerfMetricsTestsFW : DotNetPerfMetricsTests<ConsoleDynamicMethodFixtureFW>
{}

```
