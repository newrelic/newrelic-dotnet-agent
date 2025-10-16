# GitHub Copilot Instructions for New Relic .NET Agent

## Repository Overview

This repository contains the New Relic .NET Agent, which monitors .NET applications and provides performance insights. The agent supports both .NET Framework and .NET Core/.NET applications on Windows and Linux.

### Key Solutions

- **FullAgent.sln**: Main solution containing the managed C# code for the agent, including all unit tests
- **Profiler.sln**: Native profiler component that implements the .NET Profiling API
- **IntegrationTests.sln**: Integration tests for the agent (`tests/Agent/IntegrationTests/`)
- **ContainerIntegrationTests.sln**: Container-based integration tests (`tests/Agent/IntegrationTests/`)
- **UnboundedIntegrationTests.sln**: Unbounded integration tests (`tests/Agent/IntegrationTests/`)
- **BuildTools.sln**: Build tools and utilities (`build/`)

### Important Directories

- `src/Agent/`: Core agent code and home directories
- `tests/Agent/UnitTests/`: Unit tests using NUnit
- `tests/Agent/IntegrationTests/`: Integration tests that verify agent functionality
- `build/`: Build scripts and packaging configuration
- `docs/`: Development and testing documentation

## Coding Standards

### C# Style

- Always use the latest C# features and syntax available in the project
- Follow the `.editorconfig` file for formatting rules
- Use PascalCase for methods, properties, and constants
- Use camelCase for local variables (prefer `var` when possible)
- Use `_camelCase` for private fields
- All class declarations must have access modifiers
- All interfaces must be prefixed with "I" (e.g., `ITransactionName`)

### File Headers

- **Every file must include the standard license header:**
  ```csharp
  // Copyright 2020 New Relic, Inc. All rights reserved.
  // SPDX-License-Identifier: Apache-2.0
  ```
  Note: The year 2020 is specified in `.editorconfig` and should be used as-is
- Always include required `using` directives at the top of the file
- System usings should come first (per `.editorconfig`)

### Backwards Compatibility

- Code in `src/Agent/` and `src/NewRelic.Core/` must be compatible with:
  - .NET Framework 4.6.2+ 
  - .NET Standard 2.0+
- Avoid using newest-version-only features that break backwards compatibility
- The instrumentation must work with a wide range of versions of instrumented modules
- Read comments carefully - code that seems overcomplicated may be necessary for compatibility

### Comments and Documentation

- When refactoring code, preserve existing comments and documentation
- Don't add comments unless they match the style of existing comments or explain complex logic
- Complicated compatibility workarounds should be documented

## Testing

### Unit Tests

- Use NUnit framework for all unit tests
- **Always use modern NUnit assertions** (e.g., `Assert.That(actual, Is.EqualTo(expected))`)
- **Do not use reflection** to access private members of the class under test
- We use **JustMock Lite** (free version) - only use mock configurations valid for this package
- When tests use disposable resources in `[SetUp]`, always add `[TearDown]` to clean them up
- Test files should follow the naming pattern: `*Tests.cs`

### Integration Tests

- Integration tests verify agent instrumentation functionality
- PRs that add or modify instrumentation should include new or updated integration tests
- See `docs/integration-tests.md` for setup requirements
- Integration tests execute against actual New Relic accounts

### Running Tests

- Unit tests can be run from Visual Studio Test Explorer
- Build the solution first to ensure all dependencies are available
- Some integration tests require additional infrastructure (databases, IIS, etc.)

## Building

### Prerequisites

- Visual Studio 2022 with:
  - .NET desktop development workload
  - Desktop development with C++ workload
  - C++ ATL for v142 build tools (x86 & x64)

### Build Process

1. **FullAgent.sln** is the primary solution to build
2. Building creates agent home directories in `src/Agent/` for each target platform
3. The profiler is pulled from NuGet - you typically don't need to build Profiler.sln
4. See `docs/development.md` for detailed build instructions

## Important Conventions

### Security and Safety

- **Never commit secrets** to source code
- The agent runs in customer application processes - **it must not crash, destabilize, or significantly degrade performance**
- **Never leak PII** (personally identifying information) from customer applications
- Any third-party dependencies must have permissive open source licenses (e.g., MIT, Apache)

### Making Changes

- Make minimal, surgical changes - change only what's necessary
- Validate changes don't break existing behavior
- Use ecosystem tools (package managers, refactoring tools) to reduce mistakes
- Run linters and tests early and often
- For complex contributions, open a GitHub issue first to discuss with maintainers

### Git and PR Conventions

- **Always use conventional commit prefixes** for commits and pull request titles
  - We typically only use `fix`, `feat`, `chore`, `test`, and `ci`
  - Because we use Release Please, commit prefixes are important
  - Only public-facing changes important to users should be prefixed with `feat` or `fix`
  - Use `chore` for non-public-facing changes
- **Branch naming conventions:**
  - Bug fixes: `fix/**`
  - Features: `feature/**`
  - Other changes: Use appropriate prefixes like `test/**`, `chore/**`, etc.
