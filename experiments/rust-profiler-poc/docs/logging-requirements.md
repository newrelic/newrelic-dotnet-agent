# Logging System Requirements

## Why This Matters

The existing .NET Agent integration tests inspect profiler log output to verify behavior. Tests in `tests/Agent/IntegrationTests/` use `ProfilerLogFile.cs` to wait for and pattern-match against `NewRelic.Profiler.*.log` files. Any deviation in log format, file naming, or directory resolution will break these tests.

The current POC uses `log` + `env_logger`, which writes to stderr only. This is fine for early development but **must be replaced** with a compatible logging implementation before integration testing can begin.

## C++ Logger Reference

**Source**: `src/Agent/NewRelic/Profiler/Logging/Logger.h` (~296 lines)
**Tests**: `src/Agent/NewRelic/Profiler/LoggingTest/LoggerTest.cpp`

### Output Format

Each log line follows this exact format:
```
[LEVEL] YYYY-MM-DD HH:MM:SS MESSAGE
```

Level strings are fixed-width (5 chars, right-padded):
```
[Error]
[Warn ]
[Info ]
[Debug]
[Trace]
```

**Timestamp**: UTC, formatted with `strftime("%Y-%m-%d %X")` — produces `2026-02-24 14:30:45`.

**Test regex patterns** (from `LoggerTest.cpp`):
```
\[Error\] \d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d MESSAGE
\[Warn \] \d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d MESSAGE
\[Info \] \d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d MESSAGE
\[Debug\] \d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d MESSAGE
\[Trace\] \d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d MESSAGE
```

### Log File Naming

```
NewRelic.Profiler.<PID>.log
```

Where `<PID>` is the operating system process ID. Each process gets its own log file.

### Log Directory Resolution (Priority Order)

1. `NEW_RELIC_PROFILER_LOG_DIRECTORY` environment variable (profiler-specific override)
2. `NEW_RELIC_LOG_DIRECTORY` environment variable (general agent log directory)
3. Azure WebSites special case: if `HOME_EXPANDED` contains `:\DWASFiles` or `:\Sites`, use `{HOME}\LogFiles\NewRelic\`
4. `NEW_RELIC_HOME` / `CORECLR_NEWRELIC_HOME` environment variable → `{value}\Logs\` (Windows) or `{value}/logs/` (Linux)
5. Default fallback:
   - Windows: `C:\ProgramData\New Relic\.NET Agent\Logs\`
   - Linux: Common app data path + `/newrelic/`

The directory is auto-created if it doesn't exist.

### Log Level Configuration

**Environment variables** (checked in this order):
- `NEW_RELIC_LOG_LEVEL`
- `NEWRELIC_LOG_LEVEL`

**Config file** (`newrelic.config`):
```xml
<log level="info" />
```

**Supported level values** (from `Configuration.h`):
| Value | Maps to |
|-------|---------|
| `"off"` | ERROR |
| `"error"` | ERROR |
| `"warn"` | WARN |
| `"info"` | INFO (default) |
| `"debug"` | DEBUG |
| `"fine"` | DEBUG (alias) |
| `"verbose"` | TRACE |
| `"finest"` | TRACE (alias) |
| `"all"` | TRACE (alias) |

### Thread Safety

- All log writes are protected by `std::mutex` with `std::lock_guard`
- Level check happens before acquiring the mutex (fast path for filtered messages)
- Each line is flushed immediately (`std::endl`)

### Console Logging Behavior

- Console logging can be enabled alongside file logging
- When console logging is active, log level is **clamped to INFO minimum** to avoid severe performance degradation at DEBUG/TRACE levels
- Azure Function mode has additional restrictions (TRACE causes crashes)

### Scope Logging (Enter/Leave)

The C++ logger supports RAII-based scope tracking:
```
[Trace] 2026-02-24 14:30:45 Enter: Namespace::Class::Method on file.cpp(123)
[Trace] 2026-02-24 14:30:45 Leave: Namespace::Class::Method on file.cpp(123)
```

This is used for debugging profiler execution flow. Lower priority for the Rust port but worth noting.

### Lifecycle Safety

A global `volatile bool logging_available` flag guards against logging during CRT teardown. The logger destructor sets this to `false`, and all logging macros check it before proceeding.

## Key Log Messages That Tests Check For

These messages (or patterns) appear in integration tests and must be reproduced:

- Process inclusion/exclusion messages from configuration
- Module load notifications
- Instrumentation enable/disable messages
- Configuration file parsing results and errors
- Error messages for exceptional conditions

**TODO**: Catalog the exact set of log messages that integration tests match against. This requires a more detailed scan of `ProfilerLogFile.cs` usage across all integration test fixtures.

## Rust Implementation Strategy

### Recommended Approach

Do **not** use `env_logger` for production logging. Instead, implement a custom logger behind the `log` crate facade:

```
src/
└── logging/
    ├── mod.rs               # log crate facade implementation
    ├── file_logger.rs       # File output with per-PID naming
    └── log_location.rs      # Directory resolution hierarchy
```

This lets the rest of the codebase continue using `log::info!()`, `log::debug!()`, etc. while the backend handles file output, formatting, and directory resolution compatibly with the C++ implementation.

### Priority

- **POC phase**: Current `env_logger` is acceptable for development. Log output goes to stderr and is only used for debugging the POC itself.
- **Pre-integration-testing**: Must implement file-based logging with correct format and naming before any side-by-side testing or integration test validation.
- **Production**: Full compatibility including directory resolution, console clamping, Azure Function mode, and scope logging.

### What Can Be Deferred

- Azure Function mode (only needed for Azure Functions support)
- Console logging performance clamping (optimization)
- Scope logging Enter/Leave (debugging aid, not tested by integration tests)

### What Cannot Be Deferred Past Integration Testing

- File output to `NewRelic.Profiler.<PID>.log`
- `[Level] YYYY-MM-DD HH:MM:SS message` format
- Directory resolution hierarchy (at least env var overrides)
- Log level configuration from environment variables
- Thread-safe writes
