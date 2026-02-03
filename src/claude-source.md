# New Relic .NET Agent - Source Code Architecture

This document describes the source code organization and architecture of the New Relic .NET Agent.

## Overview

The agent consists of two main components:
1. **Native Profiler** - C++ code that hooks into the CLR profiling API
2. **Managed Agent** - C# code that collects telemetry and communicates with New Relic

## Directory Structure

```
src/
├── Agent/
│   ├── NewRelic/
│   │   ├── Agent/
│   │   │   ├── Core/             # Main agent implementation
│   │   │   └── Extensions/       # Framework instrumentation
│   │   ├── Profiler/             # Native profiler (C++)
│   │   └── Home/                 # Home directory project
│   ├── NewRelic.Api.Agent/       # Public API
│   ├── Configuration/            # Configuration schemas
│   ├── MsiInstaller/             # Windows installer
│   ├── newrelichome_*/           # Built agent home directories
│   └── Scripts/                  # Build scripts
└── _build/                       # Build output
```

## Core Components

### 1. Native Profiler (`Agent/NewRelic/Profiler/`)

The profiler is written in C++ and implements the .NET Profiling API to inject instrumentation bytecode.

**Key Files:**
- [Profiler/CorProfilerCallbackImpl.h](Agent/NewRelic/Profiler/Profiler/CorProfilerCallbackImpl.h) - Main profiler callback implementation
- [MethodRewriter/InstrumentFunctionManipulator.h](Agent/NewRelic/Profiler/MethodRewriter/InstrumentFunctionManipulator.h) - Bytecode injection logic
- [MethodRewriter/FunctionManipulator.h](Agent/NewRelic/Profiler/MethodRewriter/FunctionManipulator.h) - Low-level bytecode manipulation

**How It Works:**
1. Profiler attaches to CLR via `COR_ENABLE_PROFILING` environment variable
2. Sets flags to monitor JIT compilation events
3. On `JITCompilationStarted`, checks if method should be instrumented
4. Requests ReJIT for instrumented methods
5. On `ReJITCompilationStarted`, modifies bytecode to wrap method with try-catch-finally
6. Injected code calls into agent core via `AgentShim.GetFinishTracerDelegate()`

**Profiler GUIDs:**
- .NET Framework: `{71DA0A04-7777-4EC6-9643-7D28B46A8A41}`
- .NET Core/.NET: `{36032161-FFC0-4B61-B559-F6C5D41BAE5A}`

**Building:**
Use PowerShell script at [Profiler/build/build.ps1](Agent/NewRelic/Profiler/build/build.ps1):
```powershell
# Windows x64
build.ps1 -Platform x64 -Configuration Debug

# Linux (requires Docker)
build.ps1 -Platform linux
```

### 2. Agent Core (`Agent/NewRelic/Agent/Core/`)

The core agent is the heart of the monitoring solution, written in C#.

#### Core Structure

```
Core/
├── AgentHealth/              # Agent health monitoring
├── Aggregators/              # Metric and event aggregation
├── Api/                      # Internal API implementations
├── Attributes/               # Attribute collection and filtering
├── BrowserMonitoring/        # Real User Monitoring (RUM)
├── CallStack/                # Call stack tracking
├── Commands/                 # Server commands from New Relic
├── Configuration/            # Configuration management
├── DataTransport/            # Communication with New Relic
├── DistributedTracing/       # Distributed tracing implementation
├── Errors/                   # Error tracking
├── Events/                   # Event models
├── Instrumentation/          # Instrumentation coordination
├── Metrics/                  # Metric collection and aggregation
├── Samplers/                 # Periodic sampling (CPU, memory, etc.)
├── Segments/                 # Segment creation and tracking
├── Spans/                    # Span creation for distributed tracing
├── ThreadProfiling/          # Thread profiler
├── Transactions/             # Transaction management
├── TransactionTraces/        # Transaction trace generation
├── Transformers/             # Data transformation pipeline
├── Utilities/                # Helper utilities
├── Utilization/              # Host utilization detection
├── WireModels/               # Data models for New Relic protocol
└── Wrapper/                  # Wrapper base classes
```

#### Key Subsystems

##### Transaction Management (`Transactions/`)

Transactions represent units of work being monitored.

**Key Classes:**
- `Transaction` - Main transaction implementation
- `TransactionName` - Transaction naming logic
- `TransactionMetricNameMaker` - Metric name generation
- `ImmutableTransaction` - Immutable snapshot for data pipeline

**Transaction Lifecycle:**
1. Created by framework instrumentation (e.g., ASP.NET request handler)
2. Segments added as operations execute
3. Custom attributes and metadata collected
4. Transaction finished when work completes
5. Sent through data transformation pipeline
6. Aggregated into metrics, traces, events, and spans

##### Segments (`Segments/`)

Segments track individual operations within transactions.

**Segment Types:**
- External segments (HTTP calls)
- Database segments (SQL queries)
- Datastore segments (NoSQL operations)
- Message broker segments
- Custom segments

**Key Classes:**
- `Segment` - Base segment implementation
- `ExternalSegmentData` - HTTP call metadata
- `DatastoreSegmentData` - Database operation metadata
- `MessageBrokerSegmentData` - Message queue metadata

##### Distributed Tracing (`DistributedTracing/`)

Implements W3C Trace Context and New Relic distributed tracing.

**Key Classes:**
- `DistributedTracePayload` - Trace context payload
- `DistributedTracingApiModel` - API for accepting trace context
- `TracePriorityManager` - Sampling priority calculation

**Flow:**
1. Inbound: Accept trace context from headers
2. Link current transaction to parent span
3. Outbound: Inject trace context into outgoing calls
4. Generate spans for distributed tracing visualization

##### Data Transport (`DataTransport/`)

Manages communication with New Relic's data ingest services.

**Key Classes:**
- `ConnectionManager` - Manages connections to New Relic
- `DataTransportService` - Sends data to New Relic
- `AgentCommands` - Processes server-side configuration
- `HttpCollectorWire` - HTTP protocol implementation

**Data Flow:**
1. Agent collects metrics, events, traces, spans
2. Data aggregated in harvest cycle (typically 60 seconds)
3. Serialized to JSON
4. Compressed with gzip
5. Sent via HTTPS to New Relic collectors
6. Response processed for server-side configuration

##### Aggregators (`Aggregators/`)

Aggregate telemetry data before sending to New Relic.

**Key Aggregators:**
- `MetricAggregator` - Aggregates metrics
- `TransactionEventAggregator` - Transaction events
- `ErrorEventAggregator` - Error events
- `SpanEventAggregator` - Distributed tracing spans
- `CustomEventAggregator` - Custom events
- `SqlTraceAggregator` - SQL traces
- `TransactionTraceAggregator` - Transaction traces

**Harvest Cycle:**
1. Data collected during 60-second window
2. Aggregators combine and sample data
3. Data serialized and sent to New Relic
4. Aggregators reset for next cycle

##### Configuration (`Configuration/`)

Hierarchical configuration system with multiple sources.

**Configuration Sources (Priority Order):**
1. Environment variables (highest)
2. Local `newrelic.config` file
3. Server-side configuration
4. Default values (lowest)

**Key Classes:**
- `Configuration` - Main configuration model
- `ConfigurationService` - Configuration loading and updates
- `EnvironmentVariables` - Environment variable access
- `ServerConfiguration` - Server-side config from New Relic

**Important Configuration:**
- License key
- Application name
- Transaction tracer settings
- Distributed tracing settings
- Error collection settings
- Instrumentation settings

### 3. Extensions System (`Agent/NewRelic/Agent/Extensions/`)

The extensions system provides framework-specific instrumentation.

#### Extension Architecture

```
Extensions/
├── NewRelic.Agent.Extensions/    # Base extension framework
│   ├── Providers/
│   │   ├── Storage/              # Async context storage
│   │   └── Wrapper/              # Framework wrappers
```

#### Storage Providers (`Providers/Storage/`)

Manage async context across async/await boundaries.

**Implementations:**
- `AsyncLocal/` - Uses AsyncLocal<T> (.NET Core/.NET)
- `CallContext/` - Uses CallContext (.NET Framework)
- `HttpContext/` - Uses HttpContext for ASP.NET
- `OperationContext/` - Uses OperationContext for WCF
- `HybridHttpContext/` - Hybrid approach

#### Wrapper Providers (`Providers/Wrapper/`)

Instrument specific frameworks and libraries. Over 40 instrumentation providers:

**Web Frameworks:**
- `AspNet/` - ASP.NET Framework (MVC, Web Forms, etc.)
- `AspNetCore/` - ASP.NET Core 1.0-5.0
- `AspNetCore6Plus/` - ASP.NET Core 6.0+
- `Mvc3/` - ASP.NET MVC 3+
- `WebApi1/`, `WebApi2/` - ASP.NET Web API
- `Owin/` - OWIN middleware
- `OpenRasta/` - OpenRasta framework

**Databases:**
- `Sql/` - ADO.NET (SqlClient, etc.)
- `MongoDb/`, `MongoDb26/` - MongoDB drivers
- `Couchbase/`, `Couchbase3/` - Couchbase
- `CosmosDb/` - Azure Cosmos DB
- `Elasticsearch/` - Elasticsearch
- `OpenSearch/` - OpenSearch
- `ServiceStackRedis/` - ServiceStack.Redis
- `StackExchangeRedis/`, `StackExchangeRedis2Plus/` - StackExchange.Redis
- `Memcached/` - Memcached

**HTTP Clients:**
- `HttpClient/` - HttpClient
- `HttpWebRequest/` - HttpWebRequest
- `RestSharp/` - RestSharp

**Messaging:**
- `RabbitMq/` - RabbitMQ
- `Kafka/` - Kafka
- `Msmq/` - MSMQ
- `NServiceBus/` - NServiceBus
- `MassTransit/`, `MassTransitLegacy/` - MassTransit
- `AzureServiceBus/` - Azure Service Bus

**Cloud Services:**
- `AwsSdk/` - AWS SDK
- `AwsLambda/` - AWS Lambda
- `AzureFunction/` - Azure Functions
- `Bedrock/` - AWS Bedrock (AI)

**AI/ML:**
- `OpenAI/` - OpenAI SDK (GPT, etc.)
- `Bedrock/` - AWS Bedrock

**Logging:**
- `Log4NetLogging/` - log4net
- `NLogLogging/` - NLog
- `SerilogLogging/` - Serilog
- `MicrosoftExtensionsLogging/` - Microsoft.Extensions.Logging

**Other:**
- `Wcf3/` - WCF
- `WebServices/` - ASMX web services
- `ScriptHandlerFactory/` - ASP.NET AJAX
- `WebOptimization/` - ASP.NET bundling and minification

#### Creating a Wrapper

Each wrapper typically contains:
1. **Extension XML** - Defines instrumentation points
2. **Wrapper classes** - Implement instrumentation logic
3. **Tracer factories** - Create tracers for instrumented methods

**Example Structure:**
```
MyFramework/
├── MyFramework.csproj
├── MyFrameworkInstrumentation.xml
└── MyFrameworkWrapper.cs
```

**Extension XML Format:**
```xml
<extension>
  <instrumentation>
    <tracerFactory name="MyTracerFactory">
      <match assemblyName="MyFramework" className="MyClass">
        <exactMethodMatcher methodName="MyMethod" />
      </match>
    </tracerFactory>
  </instrumentation>
</extension>
```

**Wrapper Implementation:**
```csharp
public class MyFrameworkWrapper : IWrapper
{
    public AfterWrappedMethodDelegate BeforeWrappedMethod(
        InstrumentedMethodCall instrumentedMethodCall,
        IAgent agent,
        ITransaction transaction)
    {
        var segment = transaction.StartTransactionSegment(
            instrumentedMethodCall.MethodCall,
            "MyFramework");

        return Delegates.GetDelegateFor(segment);
    }
}
```

### 4. Public API (`NewRelic.Api.Agent/`)

The public API allows developers to add custom instrumentation.

**Key Interfaces:**
- `IAgent` - Access to agent functionality
- `ITransaction` - Current transaction operations
- `ISegment` - Custom segment creation
- `ISpan` - Current span access

**Static API:**
```csharp
// Located in NewRelic.Api.Agent.NewRelic class
AddCustomAttribute(string key, object value)
NoticeError(Exception exception)
SetTransactionName(string category, string name)
GetAgent()
StartAgent()
```

**Custom Instrumentation:**
```csharp
[Transaction]
public void MyBusinessMethod()
{
    // Creates a transaction if none exists
}

[Trace]
public void MyTracedMethod()
{
    // Creates a segment within transaction
}
```

### 5. Configuration Schemas (`Agent/Configuration/`)

XML schemas and related files for agent configuration.

**Key Files:**
- `newrelic.xsd` - Schema for newrelic.config
- Configuration validation and documentation

## Agent Initialization Flow

1. **Profiler Attachment**
   - CLR loads profiler DLL
   - Profiler initializes and sets event masks
   - Profiler reads instrumentation XML from extensions

2. **Agent Core Initialization**
   - AgentInitializer.Initialize() called
   - Configuration loaded from all sources
   - Services registered in dependency injection container
   - Connection to New Relic established
   - Background services started (samplers, aggregators)

3. **Instrumentation Active**
   - Methods JIT compiled with instrumentation
   - Transactions and segments created
   - Data collected and aggregated
   - Periodic harvest sends data to New Relic

## Instrumentation Workflow

1. **Method JIT Compilation**
   - Profiler intercepts JIT event
   - Checks if method matches instrumentation XML
   - Requests ReJIT if match found

2. **ReJIT Event**
   - Profiler gets original method bytecode
   - Wraps bytecode with try-catch-finally blocks
   - Injects calls to AgentShim
   - Returns modified bytecode to CLR

3. **Method Execution**
   - Try: Call GetFinishTracerDelegate (starts tracer)
   - Original method body executes
   - Catch: Report exception to tracer
   - Finally: Finish tracer with result

4. **Tracer Lifecycle**
   - Tracer created by wrapper
   - Segment/transaction started
   - Timing begins
   - Custom data collected
   - Segment/transaction finished
   - Data sent to aggregators

## Data Pipeline

```
Method Execution
    ↓
Tracer/Wrapper
    ↓
Segment/Transaction
    ↓
ImmutableTransaction
    ↓
Transformers
    ↓
Aggregators
    ↓
DataTransport
    ↓
New Relic Platform
```

## Dependency Injection

The agent uses a custom DI container for service management.

**Container:** `Core/DependencyInjection/AgentContainer`

**Key Services:**
- Configuration services
- Aggregators
- Data transport
- Samplers
- API implementations
- Metric builders

## Threading Model

- **Agent thread** - Background thread for harvesting and sampling
- **Instrumented threads** - Application threads being monitored
- **Async context** - Maintains transaction context across async boundaries

**Synchronization:**
- Lock-free data structures where possible
- ReaderWriterLockSlim for shared state
- Immutable objects for thread safety

## Performance Considerations

**Low Overhead Design:**
- Minimize allocations in hot paths
- Cache reflection results
- Fast path for non-instrumented methods
- Lazy initialization where possible
- Aggressive inlining of small methods

**Sampling:**
- Transaction traces sampled (1 per minute by default)
- Span events sampled based on priority
- Custom events reservoir sampling

## Debugging Tips

**Enable Debug Logging:**
```xml
<configuration>
  <log level="debug" />
</configuration>
```

Or environment variable:
```
NEWRELIC_LOG_LEVEL=debug
```

**Common Log Locations:**
- `{NEWRELIC_HOME}/logs/` - Agent logs
- Windows Event Viewer - Profiler errors

**Attach Debugger:**
1. Set environment variables for target app
2. Start target app
3. Attach Visual Studio debugger to process
4. Set breakpoints in agent code

**Profiler Debugging:**
- Check profiler loads: Look for "Profiler attached" in logs
- Enable profiler debug logging: See profiler documentation
- Windows Event Viewer for profiler failures

## Code Conventions

- File-scoped namespaces (recent refactoring)
- Prefer records for immutable data
- Use dependency injection for services
- Minimal public surface area
- XML documentation on public APIs
- Unit tests for all new code

## Related Documentation

- @../claude.md - Main repository guide
- @../build/claude-build.md - Build system
- @../tests/claude-tests.md - Testing guide
- [Profiler README](Agent/NewRelic/Profiler/README.md)
- [Development guide](../docs/development.md)
