# New Relic .NET Agent Repository Guide

This repository contains the New Relic .NET Agent, an Application Performance Monitoring (APM) solution for .NET applications.

## Repository Overview

The New Relic .NET Agent is a profiler-based instrumentation agent that monitors .NET applications and reports performance data to New Relic. It supports both .NET Framework and .NET Core/.NET applications on Windows and Linux.

### Key Components

1. **Profiler**: Native C++ component that uses the .NET Profiling API to inject instrumentation bytecode
2. **Agent Core**: Managed C# code that collects telemetry, manages configuration, and communicates with New Relic
3. **Extensions**: Framework-specific wrappers that instrument popular libraries and frameworks
4. **Public API**: User-facing API for custom instrumentation

## Repository Structure

```
newrelic-dotnet-agent/
├── src/               # Source code (@src/claude-source.md)
├── build/             # Build tools and packaging (@build/claude-build.md)
├── tests/             # Tests and test infrastructure (@tests/claude-tests.md)
├── docs/              # Documentation
├── deploy/            # Deployment configurations
└── .github/           # GitHub workflows and CI/CD
```

### Detailed Documentation

For comprehensive information about each area, see:
- @src/claude-source.md - Agent implementation details
- @build/claude-build.md - Build tools, packaging, and release process
- @tests/claude-tests.md - Unit and integration test structure

## Quick Start for Development

### Requirements

- Visual Studio 2022 with:
  - .NET desktop development workload
  - Desktop development with C++ workload
  - C++ ATL for v142 build tools (x86 & x64)
- Optional: Docker Desktop for Linux builds and containerized tests

### Building

The agent consists of two main solutions:

1. **FullAgent.sln** (Primary): Builds all managed code and creates platform-specific agent home directories
   - Located at repository root
   - Outputs to `src/Agent/newrelichome_*` directories
   - Build this solution for most development work

2. **Profiler.sln** (Advanced): Builds the native profiler component
   - Located at `src/Agent/NewRelic/Profiler/`
   - Only needed when modifying profiler code
   - Available as NuGet package for normal development

### Agent Home Directories

Building FullAgent.sln creates these deployment directories:

| Framework      | OS      | Arch  | Output Directory                           |
|----------------|---------|-------|--------------------------------------------|
| .NET Framework | Windows | x64   | src/Agent/newrelichome_x64                 |
| .NET Framework | Windows | x86   | src/Agent/newrelichome_x86                 |
| .NET Core      | Windows | x64   | src/Agent/newrelichome_x64_coreclr         |
| .NET Core      | Windows | x86   | src/Agent/newrelichome_x86_coreclr         |
| .NET Core      | Linux   | x64   | src/Agent/newrelichome_x64_coreclr_linux   |
| .NET Core      | Linux   | arm64 | src/Agent/newrelichome_arm64_coreclr_linux |

### Testing Locally

Configure these environment variables to attach the agent to a process:

**For .NET Framework:**
```bash
NEWRELIC_LICENSE_KEY=<your license key>
NEWRELIC_HOME=path\to\home\directory
COR_ENABLE_PROFILING=1
COR_PROFILER={71DA0A04-7777-4EC6-9643-7D28B46A8A41}
COR_PROFILER_PATH=path\to\home\directory\NewRelic.Profiler.dll
```

**For .NET Core/.NET:**
```bash
NEWRELIC_LICENSE_KEY=<your license key>
CORECLR_NEWRELIC_HOME=path\to\home\directory
CORECLR_ENABLE_PROFILING=1
CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A}
CORECLR_PROFILER_PATH=path\to\home\directory\NewRelic.Profiler.dll
```

## How the Agent Works

### Profiler Attachment Process

1. CLR checks `COR_ENABLE_PROFILING` / `CORECLR_ENABLE_PROFILING` environment variable
2. Loads profiler DLL from `COR_PROFILER_PATH` / `CORECLR_PROFILER_PATH`
3. Profiler subscribes to JIT compilation events
4. When methods are JIT compiled, profiler modifies bytecode to inject instrumentation
5. Injected bytecode calls into agent core to create tracers and record telemetry

### Instrumentation Model

The profiler wraps instrumented methods with try-catch-finally blocks that:
1. Call `AgentShim.GetFinishTracerDelegate()` to start timing
2. Execute the original method body
3. Handle exceptions and record errors
4. Finish the tracer with result/exception information

### Extension System

The agent uses an XML-based extension system to define instrumentation points:
- Extension files in `extensions/` directory define which methods to instrument
- Profiler reads extensions and identifies target methods during JIT compilation
- Agent can refresh instrumentation at runtime when extensions change

## Key Architecture Concepts

### Transactions
A transaction represents a single unit of work (e.g., web request, background job). Transactions:
- Track timing and performance metrics
- Contain segments representing nested operations
- Generate transaction traces when slow
- Create transaction events for analytics

### Segments
Segments represent individual operations within a transaction:
- External HTTP calls
- Database queries
- Custom instrumentation
- Framework operations

### Spans
Distributed tracing spans provide detailed timing information:
- Created for transactions and segments
- Linked across services via trace context
- Sent to New Relic for distributed tracing visualization

### Metrics
Aggregated performance measurements collected over time:
- Transaction metrics (throughput, response time, error rate)
- External call metrics
- Database metrics
- Custom metrics

## Configuration

Agent configuration sources (in priority order):
1. Environment variables (highest priority)
2. `newrelic.config` XML file
3. Server-side configuration from New Relic
4. Default values

Key configuration locations:
- [src/Agent/NewRelic/Agent/Core/Configuration](src/Agent/NewRelic/Agent/Core/Configuration) - Configuration models
- [src/Agent/NewRelic/Agent/Core/Config](src/Agent/NewRelic/Agent/Core/Config) - Configuration loading

## Important Files and Locations

- [FullAgent.sln](FullAgent.sln) - Main solution file
- [src/Agent/CHANGELOG.md](src/Agent/CHANGELOG.md) - Release notes
- [docs/development.md](docs/development.md) - Development guide
- [docs/integration-tests.md](docs/integration-tests.md) - Integration test documentation
- [CONTRIBUTING.md](CONTRIBUTING.md) - Contribution guidelines

## Common Development Tasks

### Adding New Instrumentation

1. Create or modify extension XML in `src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/<Framework>/`
2. Implement wrapper classes that create tracers
3. Add integration tests in `tests/Agent/IntegrationTests/`
4. Update documentation

### Debugging the Agent

1. Set `NEWRELIC_LOG_LEVEL=debug` environment variable
2. Check logs in agent home directory's `logs/` folder
3. Use Visual Studio debugger attached to instrumented process
4. For profiler issues, check Windows Event Viewer

### Running Tests

- **Unit Tests**: Use Visual Studio Test Explorer
- **Integration Tests**: See @tests/claude-tests.md
- Some integration tests require infrastructure (databases, message queues)

## Common Code Patterns

### Creating Transactions

Transactions are typically created by framework instrumentation:
- ASP.NET/ASP.NET Core wrappers create web transactions
- Background frameworks create non-web transactions
- Custom transactions via public API

### Creating Segments

Segments are created within transactions:
1. Wrapper creates a tracer via the instrumentation wrapper interface
2. Tracer records timing and metadata
3. Tracer is finished when operation completes

### Recording Custom Data

Use the public API in `src/Agent/NewRelic.Api.Agent/`:
- `IAgent.CurrentTransaction` - Access current transaction
- `ITransaction.AddCustomAttribute()` - Add custom attributes
- `NewRelic.Api.Agent.NewRelic.*` - Static helper methods

## Coding Standards

The .NET agent team follows coding standards that should be adhered to when generating or modifying code. These are best practices and encouraged guidelines rather than absolute requirements.

### C# / Managed Agent

**Indentation:**
- Use spaces for indentation (Visual Studio default)
- Follow Visual Studio default settings for number of spaces per indentation level

**Naming Conventions:**
- **Types**: Use type aliases (`int`, `string`) over BCL types (`Int32`, `String`)
- **Private fields**: `_camelCase` with underscore prefix
- **Public fields**: `PascalCase`
- **Local variables**: `camelCase`, use `var` when possible
- **Classes**: `PascalCase`, singular (plural for collections)
- **Interfaces**: `PascalCase` with `I` prefix (e.g., `IAttributeFilter`)
- **Methods**: `PascalCase`, descriptive action names
- **Method parameters**: `camelCase`, named after their type

**Class Structure (in order):**
1. Fields (const, static readonly, readonly, static, private, public)
2. Properties
3. Constructors
4. Methods
5. Events

**Code Organization:**
- Fields declared and initialized at top of class
- All declarations must have explicit access modifiers
- Avoid multiple optional boolean parameters (use named parameters or overloads)
- Line breaks between all code blocks

**Example:**
```csharp
public class TransactionProcessor
{
	// Fields first
	private const int MaxRetries = 3;
	private readonly ILogger _logger;
	private static int _instanceCount = 0;

	// Properties second
	public int TransactionCount { get; private set; }

	// Constructor third
	public TransactionProcessor(ILogger logger)
	{
		_logger = logger;
		_instanceCount++;
	}

	// Methods fourth
	public void ProcessTransaction(string transactionName)
	{
		var transaction = CreateTransaction(transactionName);
		// ...
	}
}
```

### C++ / Profiler

- Follow [WebKit C++ style guide](https://webkit.org/code-style-guidelines/)
- Use compact namespaces: `namespace NewRelic::Profiler::Logger { ... }`
- Formatting defined in `.clang-format` at solution root
- ReSharper settings in `NewRelic.Profiler.sln.DotSettings`

### General Practices

**Testing:**
- Unit tests required for all new code
- Near 100% code coverage for shared libraries
- Test all behavior including exceptions

**Documentation:**
- XML documentation on public APIs
- Clear, descriptive naming to reduce need for comments
- Comment only when logic isn't self-evident

**Code Quality:**
- Minimal public surface area
- Prefer dependency injection for services
- Prefer records for immutable data
- Use file-scoped namespaces (recent standard)

**Important:** When Claude generates or modifies code, these standards should be followed to maintain consistency with the existing codebase.

## Development Workflow

1. Create feature branch from `main`
2. Make changes and add tests
3. Run unit tests locally
4. Build FullAgent.sln successfully
5. Test locally with sample application
6. Submit pull request
7. CI runs all tests and checks
8. Review and merge

## Release Process

Releases are managed via release-please:
- Conventional commits drive version bumping
- Release notes auto-generated from commits
- See [release-please](release-please/) directory

## Getting Help

- **Documentation**: [docs.newrelic.com](https://docs.newrelic.com/docs/agents/net-agent/)
- **Community**: [New Relic Community Forum](https://forum.newrelic.com/)
- **Issues**: GitHub Issues in this repository
- **Development Questions**: Check existing documentation in `docs/`

## License

Apache 2.0 - See [LICENSE](LICENSE)
