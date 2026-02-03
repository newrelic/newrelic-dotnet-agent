# New Relic .NET Agent - Testing Guide

This document describes the testing infrastructure and strategies for the New Relic .NET Agent.

## Overview

The test suite consists of:
- **Unit Tests** - Fast, isolated tests of individual components
- **Integration Tests** - End-to-end tests with real applications
- **Container Tests** - Tests running in Docker containers
- **Unbounded Tests** - Tests requiring external infrastructure

## Directory Structure

```
tests/
├── Agent/
│   ├── UnitTests/                    # Unit tests
│   │   ├── Core.UnitTest/            # Core agent tests
│   │   ├── NewRelic.Agent.Extensions.Tests/  # Extension tests
│   │   ├── CompositeTests/           # Cross-component tests
│   │   ├── NewRelic.Agent.TestUtilities/     # Test helpers
│   │   ├── AsyncLocalTests/          # Async context tests
│   │   ├── ParsingTests/             # Parser tests
│   │   └── PublicApiChangeTests/     # API compatibility tests
│   ├── IntegrationTests/             # Integration tests
│   │   ├── IntegrationTests/         # Main integration tests
│   │   ├── ContainerIntegrationTests/        # Docker-based tests
│   │   ├── UnboundedIntegrationTests/        # Infrastructure tests
│   │   ├── Applications/             # Test applications
│   │   ├── SharedApplications/       # Shared test apps
│   │   ├── UnboundedApplications/    # Apps for unbounded tests
│   │   ├── ContainerApplications/    # Apps for container tests
│   │   ├── IntegrationTestHelpers/   # Test utilities
│   │   ├── ApplicationHelperLibraries/       # Helper libraries
│   │   └── Models/                   # Test data models
│   ├── Shared/                       # Shared test code
│   │   ├── TestSerializationHelpers/ # Serialization test utils
│   │   └── TestSerializationHelpers.Test/    # Tests for test utils
│   └── NewRelic.Testing.Assertions/  # Custom assertions
└── NewRelic.Core.Tests/              # Core library tests
```

## Unit Tests

Unit tests focus on testing individual classes and methods in isolation.

### Location

All unit tests are in [Agent/UnitTests/](Agent/UnitTests/)

### Key Test Projects

#### Core.UnitTest

Tests for the main agent core functionality.

**Test Coverage:**
- Transaction management
- Segment creation
- Metric aggregation
- Configuration loading
- Data serialization
- Distributed tracing
- Error handling
- API implementations

**Example Test Structure:**
```csharp
[TestFixture]
public class TransactionTests
{
    [Test]
    public void Transaction_HasCorrectName()
    {
        // Arrange
        var transaction = CreateTransaction();

        // Act
        transaction.SetWebTransactionName("Category", "Name");

        // Assert
        Assert.That(transaction.TransactionName.Name, Is.EqualTo("Name"));
    }
}
```

#### NewRelic.Agent.Extensions.Tests

Tests for extension providers and wrappers.

**Test Coverage:**
- Wrapper implementations
- Storage providers
- Instrumentation logic
- Framework-specific behavior

#### CompositeTests

Tests that span multiple components or test complex interactions.

**Test Coverage:**
- Agent initialization
- End-to-end workflows
- Configuration scenarios
- Cross-cutting concerns

#### AsyncLocalTests

Specialized tests for async context management.

**Test Coverage:**
- AsyncLocal behavior
- CallContext behavior
- Context flow across async/await
- Thread local storage

#### ParsingTests

Tests for parsing and serialization logic.

**Test Coverage:**
- SQL parsing
- Configuration parsing
- JSON serialization
- Protocol parsing

#### PublicApiChangeTests

Tests that detect breaking changes to public API.

**Purpose:**
- Prevent accidental API changes
- Enforce semantic versioning
- Document API surface

**Approach:**
- Compares current API to baseline
- Fails if breaking changes detected
- Must be explicitly updated for intentional changes

### Running Unit Tests

**Visual Studio:**
1. Open Test Explorer (Test > Test Explorer)
2. Click "Run All Tests"
3. Or right-click project/test and "Run Tests"

**Command Line:**
```bash
dotnet test tests/Agent/UnitTests/Core.UnitTest/Core.UnitTest.csproj
```

**Run All Unit Tests:**
```bash
dotnet test --filter "FullyQualifiedName~UnitTest"
```

### Test Frameworks

The agent test suite uses multiple testing frameworks:

- **NUnit** - Primary test framework for unit tests
- **XUnit** - Used for integration tests and some unit tests
- Custom assertions in `NewRelic.Testing.Assertions`

**When to use each:**
- **NUnit**: Default choice for unit tests, provides rich assertion library
- **XUnit**: Preferred for integration tests, better isolation between tests (creates new instance per test)

### Mocking Framework

- **Telerik JustMock Lite** - Free version of JustMock used for mocking
- **Important limitation**: Tests can only use features available in the free JustMock Lite version
- **What's available**: Mock interfaces, virtual methods, properties
- **Not available**: Mock sealed classes, non-virtual methods, static methods (these require paid version)

**Example:**
```csharp
// JustMock Lite - creating mocks
var mockAgent = Mock.Create<IAgent>();
var mockTransaction = Mock.Create<ITransaction>();

// Arranging behavior
Mock.Arrange(() => mockAgent.CurrentTransaction).Returns(mockTransaction);

// Asserting
Mock.Assert(() => mockTransaction.AddCustomAttribute("key", "value"), Occurs.Once());
```

**Design consideration**: When writing testable code, prefer interfaces and virtual methods to ensure they can be mocked with JustMock Lite.

### Testing Utilities

#### NewRelic.Agent.TestUtilities

Common utilities for unit tests:
- Mock builders
- Test data generators
- Helper methods
- Configuration builders

**Example:**
```csharp
var mockAgent = Mock.Create<IAgent>();
var transaction = new TransactionBuilder()
    .WithName("MyTransaction")
    .WithSegments(3)
    .Build();
```

#### NewRelic.Testing.Assertions

Custom assertions for agent-specific validation:
- Metric assertions
- Transaction trace assertions
- Event assertions
- Span assertions

**Example:**
```csharp
MetricAssertions.ExpectMetric(metrics, "WebTransaction/MVC/Home/Index");
```

## Integration Tests

Integration tests run the agent against real applications and verify end-to-end behavior.

### Test Types

1. **Regular Integration Tests** - Tests that run on host machine
2. **Container Integration Tests** - Tests that run in Docker
3. **Unbounded Integration Tests** - Tests requiring external infrastructure

### Integration Test Structure

#### Test Applications ([IntegrationTests/Applications/](Agent/IntegrationTests/Applications/))

Real applications instrumented by the agent:
- ASP.NET Framework applications
- ASP.NET Core applications
- Console applications
- Windows Services
- Azure Functions
- AWS Lambda functions

**Example Applications:**
- `BasicMvcApplication` - Simple ASP.NET MVC app
- `AspNetCoreBasicWebApiApplication` - ASP.NET Core API
- `ConsoleMultiFunctionApplicationFW` - Multi-framework console app
- `Owin2WebApi` - OWIN-based application
- Many framework-specific test apps

**Application Structure:**
Each test application:
- Is a real, runnable application
- Includes instrumented code paths
- Has endpoints/methods that trigger specific behavior
- Can be started programmatically by tests

#### Integration Tests ([IntegrationTests/IntegrationTests/](Agent/IntegrationTests/IntegrationTests/))

Test classes that exercise the applications.

**Test Pattern:**
1. Start test application with agent attached
2. Exercise application (HTTP requests, method calls, etc.)
3. Wait for agent to harvest data
4. Retrieve agent data from application
5. Assert on metrics, traces, events, spans

**Example Test:**
```csharp
[TestFixture]
public class BasicMvcTests : NewRelicIntegrationTest<AspNetFrameworkBasicMvcApplication>
{
    private readonly AspNetFrameworkBasicMvcApplication _fixture;

    public BasicMvcTests()
    {
        _fixture = new AspNetFrameworkBasicMvcApplication();
    }

    [Test]
    public void HomeIndexCreatesTransaction()
    {
        // Act
        var result = _fixture.Get("Home/Index");

        // Assert
        var metrics = _fixture.AgentLog.GetMetrics();

        Assert.Multiple(() =>
        {
            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(metrics, Does.Contain("WebTransaction/MVC/Home/Index"));
        });
    }
}
```

### Integration Test Helpers

#### IntegrationTestHelpers

Utilities for integration tests:
- Application fixture base classes
- HTTP client helpers
- Agent log parsing
- Metric/event/trace extractors
- Timing utilities

**Key Classes:**
- `RemoteApplication` - Base class for test applications
- `AgentLogFile` - Parses agent logs
- `MetricWireModel` - Metric data model
- `TransactionTraceWireModel` - Transaction trace model
- `SpanEventWireModel` - Span event model

#### Application Helpers ([ApplicationHelperLibraries/](Agent/IntegrationTests/ApplicationHelperLibraries/))

Libraries included in test applications:
- `ConsoleDynamicMethodFixtureFWLatest` - Dynamic method testing
- `LoggingHelpers` - Logging instrumentation testing
- Test data generators

### Running Integration Tests

**Prerequisites:**
- Agent built (FullAgent.sln)
- Agent home directories present in `src/Agent/newrelichome_*`
- Docker Desktop (for container tests)
- External services (for unbounded tests)

**Visual Studio:**
1. Set integration test project as startup project
2. Run specific tests from Test Explorer

**Command Line:**
```bash
# Run all integration tests
dotnet test tests/Agent/IntegrationTests/IntegrationTests/IntegrationTests.csproj

# Run specific test
dotnet test --filter "FullyQualifiedName~BasicMvcTests"

# Run category
dotnet test --filter "Category=AspNetCore"
```

**PowerShell Script:**
```powershell
.\build\Scripts\run-integration-tests.ps1
```

### Container Integration Tests

Tests that run applications in Docker containers.

**Location:** [IntegrationTests/ContainerIntegrationTests/](Agent/IntegrationTests/ContainerIntegrationTests/)

**Purpose:**
- Test Linux agent
- Test in isolated environment
- Test container-specific scenarios
- Test with different base images

**Container Applications:** [IntegrationTests/ContainerApplications/](Agent/IntegrationTests/ContainerApplications/)

**Example:**
```csharp
[TestFixture]
public class AspNetCoreDistTracingTests : NewRelicIntegrationTest<AspNetCoreDistTracingApplication>
{
    private readonly AspNetCoreDistTracingApplication _fixture;

    public AspNetCoreDistTracingTests()
    {
        _fixture = new AspNetCoreDistTracingApplication();
    }

    [Test]
    public void DistributedTracingWorksInContainer()
    {
        // Test distributed tracing in containerized app
    }
}
```

**Running:**
```bash
dotnet test tests/Agent/IntegrationTests/ContainerIntegrationTests/ContainerIntegrationTests.csproj
```

### Unbounded Integration Tests

Tests requiring external infrastructure (databases, message queues, etc.).

**Location:** [IntegrationTests/UnboundedIntegrationTests/](Agent/IntegrationTests/UnboundedIntegrationTests/)

**Infrastructure:**
- MySQL
- PostgreSQL
- Microsoft SQL Server
- MongoDB
- Redis
- RabbitMQ
- Kafka
- Elasticsearch
- Couchbase
- And more...

**Setup:**
Some tests use docker-compose to start infrastructure:
```yaml
# Example docker-compose.yml
services:
  mysql:
    image: mysql:8.0
    environment:
      MYSQL_ROOT_PASSWORD: password
  redis:
    image: redis:6
  rabbitmq:
    image: rabbitmq:3-management
```

**Running:**
```bash
# Start infrastructure
docker-compose up -d

# Run unbounded tests
dotnet test tests/Agent/IntegrationTests/UnboundedIntegrationTests/UnboundedIntegrationTests.csproj
```

### Integration Test Configuration

Tests use configuration files to control agent behavior:

**newrelic.config:**
Each test application can have custom configuration:
```xml
<configuration>
  <service licenseKey="INTEGRATION_TEST_KEY"/>
  <application>
    <name>IntegrationTestApp</name>
  </application>
  <transactionTracer enabled="true"/>
  <distributedTracing enabled="true"/>
</configuration>
```

**Environment Variables:**
Tests set environment variables to configure agent:
```csharp
fixture.SetEnvironmentVariable("NEW_RELIC_LICENSE_KEY", "test_key");
fixture.SetEnvironmentVariable("NEW_RELIC_HOST", "fake_collector");
```

## Test Data Models

### Models Project ([IntegrationTests/Models/](Agent/IntegrationTests/Models/))

Data models for agent telemetry:
- `MetricWireModel` - Metrics sent to New Relic
- `TransactionTraceWireModel` - Transaction traces
- `TransactionEventWireModel` - Transaction events
- `ErrorEventWireModel` - Error events
- `SpanEventWireModel` - Span events
- `CustomEventWireModel` - Custom events
- `LogEventWireModel` - Log events

**Purpose:**
- Deserialize agent output
- Strongly-typed assertions
- Schema validation

## Testing Best Practices

### Unit Tests

**Do:**
- Test one thing per test
- Use descriptive test names
- Arrange-Act-Assert pattern
- Mock external dependencies using JustMock Lite
- Test edge cases and error conditions
- Keep tests fast
- Design code with interfaces and virtual methods for mockability

**Don't:**
- Access file system or network
- Depend on test execution order
- Use Thread.Sleep (use synchronization)
- Test framework code (trust the framework)
- Create code that requires paid mocking features (sealed classes, static methods, non-virtual methods)

### Integration Tests

**Do:**
- Test realistic scenarios
- Verify end-to-end behavior
- Test agent with real frameworks
- Assert on actual telemetry
- Clean up resources
- Use appropriate timeouts

**Don't:**
- Make tests flaky
- Depend on external services (use unbounded tests)
- Skip error handling
- Hard-code timing assumptions

### Test Naming

**Convention:**
```
MethodName_Scenario_ExpectedBehavior
```

**Examples:**
- `CreateTransaction_WithWebRequest_SetsWebTransactionName`
- `GetMetrics_WhenNoData_ReturnsEmptyCollection`
- `DistributedTracing_AcrossTwoServices_CreatesConnectedSpans`

## Debugging Tests

### Unit Tests

1. Set breakpoint in test
2. Right-click test in Test Explorer
3. Select "Debug Test"
4. Step through code

### Integration Tests

1. Set breakpoint in test or agent code
2. Debug integration test
3. Agent runs in-process, so breakpoints work
4. Check agent logs in test output

**Agent Logs:**
Tests typically capture agent logs:
```csharp
var logs = _fixture.AgentLog.GetFileLines();
foreach (var log in logs)
{
    Console.WriteLine(log);
}
```

### Container Tests

1. Debug flag can make container wait for attach
2. Remote debugging into container
3. View container logs: `docker logs <container-id>`

## Test Infrastructure

### Fake Collector

Integration tests use a fake New Relic collector:
- Receives agent data
- Returns configuration
- Allows data retrieval for assertions
- No actual data sent to New Relic

### Test Fixtures

**Application Fixtures:**
- Manage application lifecycle
- Start/stop applications
- Configure agent
- Collect telemetry
- Provide helper methods

**Example Lifecycle:**
1. Constructor: Configure application
2. Test Setup: Start application and agent
3. Test: Exercise application
4. Test Teardown: Stop application
5. Dispose: Clean up resources

## Code Coverage

Code coverage tracked via Codecov.

**Running with Coverage:**
```bash
dotnet test --collect:"XPlat Code Coverage"
```

**Coverage Report:**
- Uploaded to Codecov automatically by CI
- Badge in README shows coverage percentage
- PRs show coverage changes

## Continuous Integration

### GitHub Actions

Tests run automatically on:
- Every push
- Every pull request
- Scheduled (nightly)

**Workflows:**
- [../.github/workflows/all_solutions.yml](../.github/workflows/all_solutions.yml) - Main build and test workflow

**Test Matrix:**
- Multiple .NET versions
- Multiple operating systems
- Multiple configurations

**Test Results:**
- Published as workflow artifacts
- Visible in PR checks
- Failed tests block merge

## Performance Tests

While not a dedicated performance test suite, integration tests measure:
- Agent overhead
- Memory usage
- Instrumentation impact

**Monitoring:**
- Watch for degradation in test execution time
- Profile slow tests
- Optimize hot paths

## Testing New Instrumentation

When adding new instrumentation:

1. **Add Unit Tests**
   - Test wrapper logic in isolation
   - Test error handling
   - Test configuration options

2. **Create Test Application**
   - New application or modify existing
   - Exercise instrumented code path
   - Include edge cases

3. **Add Integration Test**
   - Start application with agent
   - Trigger instrumented behavior
   - Assert on telemetry:
     - Correct metrics created
     - Spans have right attributes
     - Transaction names correct
     - No agent errors

4. **Test Across Versions**
   - Test with multiple framework versions
   - Test backward compatibility
   - Test forward compatibility

## Troubleshooting Tests

### Flaky Tests

**Common Causes:**
- Timing assumptions
- Shared state between tests
- External dependencies
- Port conflicts

**Solutions:**
- Use proper synchronization
- Isolate test state
- Mock external dependencies
- Use random/available ports

### Integration Test Failures

**Check:**
1. Agent built successfully?
2. Agent home directories present?
3. Environment variables set correctly?
4. Ports available?
5. Agent logs for errors
6. Test application logs

**Agent Logs Location:**
Tests typically put logs in temp directory. Check test output for path.

### Container Test Failures

**Check:**
1. Docker Desktop running?
2. Sufficient Docker resources?
3. Image built successfully?
4. Container started?
5. Network connectivity?

**Debug:**
```bash
# List containers
docker ps -a

# View container logs
docker logs <container-id>

# Exec into container
docker exec -it <container-id> /bin/bash
```

## Test Configuration

### test.runsettings

Repository root contains [../test.runsettings](../test.runsettings) for test configuration:
- Test timeout
- Parallelization
- Data collectors
- Environment variables

**Usage:**
```bash
dotnet test --settings test.runsettings
```

## Related Documentation

- @../claude.md - Main repository guide
- @../src/claude-source.md - Source code architecture
- @../build/claude-build.md - Build system
- [Integration tests documentation](../docs/integration-tests.md)
- [Development guide](../docs/development.md)
