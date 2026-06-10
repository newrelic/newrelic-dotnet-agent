# Design: OTLP Export Supportability Metrics Integration Tests

**Date:** 2026-06-10
**Branch:** `chore/otlp-export-supportability-metrics`
**Ticket:** NR-574460

## Context

PR #3628 adds three supportability metrics to the OTLP metrics bridge:

- `Supportability/Metrics/DotNet/OpenTelemetryBridge/export/success` — emitted on each successful OTLP export
- `Supportability/Metrics/DotNet/OpenTelemetryBridge/export/retry` — emitted on each retry attempt
- `Supportability/Metrics/DotNet/OpenTelemetryBridge/export/failure` — emitted after all retries are exhausted

These metrics are emitted by `CustomRetryHandler`, which is only active on `#if NETSTANDARD2_0_OR_GREATER` (.NET Core/.NET 5+ targets). .NET Framework targets bypass the retry handler and will not emit these metrics; Framework coverage is deferred to a future ticket.

The existing `OpenTelemetryMetricsTests` verify application metric bridging only; no integration test currently asserts on these agent health metrics.

---

## Scope

Three test scenarios, .NET Core only:

1. **Happy path** — mock always returns 200; assert `export/success` recorded, retry/failure absent.
2. **Retry-then-succeed** — mock fails first 2 attempts, succeeds on attempt 3; assert retry and success recorded, failure absent.
3. **Full exhaustion** — mock fails indefinitely; assert failure and retries recorded, success absent.

---

## Component 1: Mock Collector Failure Mode (`OtlpMetricsController`)

**File:** `tests/Agent/IntegrationTests/Applications/MockNewRelic/Controllers/OtlpMetricsController.cs`

Add two fields:

```csharp
private int _failuresRemaining;   // -1 = infinite, 0 = none, N = N more failures
private int _failureStatusCode;   // HTTP status code to return when failing
```

Both are read/written via `Interlocked` for thread safety.

**Modify `POST /v1/metrics`:** Before normal processing, check `_failuresRemaining`. If non-zero, return the configured status code with an empty body; if `_failuresRemaining > 0` (not indefinite), atomically decrement it. If `_failuresRemaining == -1`, do not decrement (stay in infinite failure mode). Otherwise proceed normally.

**New endpoint `POST /v1/metrics/configure-failures`:** Accepts JSON body:

```json
{ "statusCode": 503, "count": 2 }
```

- `count: 0` — disables failure mode
- `count: -1` — fail indefinitely
- `count: N` — fail next N requests then revert to 200

**Modify `POST /v1/metrics/clear`:** Also resets `_failuresRemaining = 0` and `_failureStatusCode = 0`, so clearing metrics state always clears failure state too.

---

## Component 2: Fixture API (`OtlpMetricsWithCollectorFixtureBase`)

**File:** `tests/Agent/IntegrationTests/IntegrationTests/RemoteServiceFixtures/OTLPMetricsWithCollectorFixture.cs`

Add two public methods:

```csharp
public void ConfigureOtlpFailures(int statusCode, int count)
// POSTs { statusCode, count } to /v1/metrics/configure-failures

public void ClearOtlpFailures()
// POSTs { statusCode: 0, count: 0 } to /v1/metrics/configure-failures
```

`ClearCollectedOTLPMetrics()` calls `ClearOtlpFailures()` internally so state stays consistent.

**Configuration applied in all new test classes (via `setupConfiguration`):**

```csharp
configModifier.ConfigureFasterMetricsHarvestCycle(10);       // 10s harvest
configModifier.SetOpenTelemetryMetricsExportInterval(5000);  // 5s export interval
configModifier.SetOpenTelemetryMetricsExportTimeout(4000);   // 4s timeout (must be < interval)
```

**Wait helper (used in `exerciseApplication`):** Poll `AgentLog.GetMetrics()` for a named metric up to a 30s timeout with 500ms sleep, following the pattern in `RequiredSupportabilityMetrics.cs`.

---

## Component 3: New Test File

**File:** `tests/Agent/IntegrationTests/IntegrationTests/OpenTelemetry/OpenTelemetryExportSupportabilityMetricsTests.cs`

Three abstract base classes, each with `CoreLatest` and `CoreNet8` concrete subclasses.

### Scenario 1: `OtlpExportSuccessMetricsTestsBase`

No failure mode configured. `exerciseApplication` waits for `export/success` to appear in agent metrics (up to 30s).

**Assertions:**
- `export/success` — callCount ≥ 1
- `export/retry` — absent
- `export/failure` — absent

### Scenario 2: `OtlpExportRetryMetricsTestsBase`

`exerciseApplication` calls `ConfigureOtlpFailures(503, 2)` then waits for `export/success`.

Timing: first export fires at ~5s; attempt 1 fails, 1s delay, attempt 2 fails, 2s delay, attempt 3 succeeds. Total ~8s for first success. Harvest at 10s captures the metrics.

**Assertions:**
- `export/retry` — callCount ≥ 2
- `export/success` — callCount ≥ 1
- `export/failure` — absent

All counts use `≥` because the harvest window may capture multiple export cycles.

### Scenario 3: `OtlpExportFailureMetricsTestsBase`

`exerciseApplication` calls `ConfigureOtlpFailures(503, -1)` then waits for `export/failure` to appear (up to 30s).

**Assertions:**
- `export/failure` — callCount ≥ 1
- `export/retry` — callCount ≥ 2
- `export/success` — absent

### Concrete subclasses (all three scenarios)

```csharp
public class OtlpExportSuccessMetricsTestsCoreLatest
    : OtlpExportSuccessMetricsTestsBase<OtlpMetricsWithCollectorFixtureCoreLatest> { ... }

public class OtlpExportSuccessMetricsTestsCoreNet8
    : OtlpExportSuccessMetricsTestsBase<OtlpMetricsWithCollectorFixtureCoreNet8> { ... }

// same pattern for Retry and Failure scenarios
```

Six concrete test classes total (3 scenarios × 2 framework targets).

---

## Timing Budget (Failure Scenario, Worst Case)

| Phase | Duration |
|---|---|
| Agent connect | ~10s |
| First OTLP export fires | +5s |
| Retry delay 1 (attempt 1 → 2) | +1–1.5s |
| Retry delay 2 (attempt 2 → 3) | +2–2.5s |
| Harvest cycle | +10s |
| Poll loop margin | up to 30s |
| **Total** | **~58s** |

Well within xUnit's default per-test timeout.

---

## Files Changed

| File | Change |
|---|---|
| `Applications/MockNewRelic/Controllers/OtlpMetricsController.cs` | Add failure mode fields + configure endpoint + clear reset |
| `IntegrationTests/RemoteServiceFixtures/OTLPMetricsWithCollectorFixture.cs` | Add `ConfigureOtlpFailures` / `ClearOtlpFailures` methods |
| `IntegrationTests/OpenTelemetry/OpenTelemetryExportSupportabilityMetricsTests.cs` | New file — 3 base classes, 6 concrete classes |

No changes to production agent code. No new test applications needed.
