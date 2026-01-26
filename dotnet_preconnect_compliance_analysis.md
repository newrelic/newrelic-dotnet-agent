# .NET Agent Preconnect Compliance Analysis

| Metadata | Value |
|----------|-------|
| **Date** | 2026-01-26 |
| **Agent** | .NET Agent |
| **Specification** | Preconnect |
| **Spec Version** | 01/2018 |
| **Repository** | newrelic-dotnet-agent |

## Executive Summary

**Overall Compliance: ✅ 100% (Excellent)**

The .NET agent fully implements all Preconnect specification requirements. License key parsing correctly extracts region identifiers using the specified regex, hostname construction follows the exact algorithm, local configuration properly takes precedence, and preconnect requests are sent to the correct host.

**Key Strengths:**
- ✅ Correct regex pattern `^.+?x` for parsing region identifiers
- ✅ Proper stripping of trailing 'x' characters and hostname construction
- ✅ Local configuration precedence over region-based hostnames
- ✅ Preconnect requests never sent to previous redirect_host
- ✅ Redirect host from preconnect used for subsequent connections
- ✅ Cross-agent test suite validates all scenarios

**Areas for Improvement:**
- None - implementation is fully compliant

**Compliance Breakdown by Category:**
- License Key Parsing: 100% ✅
- Hostname Construction: 100% ✅
- Preconnect Behavior: 100% ✅
- Local Configuration: 100% ✅
- Testing: 100% ✅


## Detailed Requirements Analysis

### 1. License Key Parsing

#### ✅ MUST parse license key using regex `^.+?x`

**Spec Requirement:** "agents MUST parse the license key given by the customer for a region identifier using the regex pattern ^.+?x"

**Implementation:** [ConnectionInfo.cs:37](src/Agent/NewRelic/Agent/Core/DataTransport/ConnectionInfo.cs#L37)

```csharp
private static readonly Regex accountRegionRegex = new Regex("^.+?x");
```

The regex is compiled once as a static readonly field and applied to the license key at [ConnectionInfo.cs:74](src/Agent/NewRelic/Agent/Core/DataTransport/ConnectionInfo.cs#L74).

#### ✅ MUST strip trailing 'x' characters

**Spec Requirement:** "agents MUST strip trailing x characters from the matched identifier"

**Implementation:** [ConnectionInfo.cs:78](src/Agent/NewRelic/Agent/Core/DataTransport/ConnectionInfo.cs#L78)

```csharp
var regionSegment = match.Value.TrimEnd(regionSeparator);
```

Uses `TrimEnd('x')` to remove all trailing 'x' characters, handling both `eu01x` and `eu01xx` formats.

#### ✅ MUST insert region between "collector." and ".nr-data.net"

**Spec Requirement:** "agents MUST strip trailing x characters from the matched identifier and insert the result between collector. and .nr-data.net"

**Implementation:** [ConnectionInfo.cs:79-80](src/Agent/NewRelic/Agent/Core/DataTransport/ConnectionInfo.cs#L79-L80)

```csharp
var collectorUrlRegionStartPosition = regionAwareDefaultCollectorUrl.IndexOf(domainSeparator) + 1;
var regionAwareCollectorUrl = regionAwareDefaultCollectorUrl.Insert(collectorUrlRegionStartPosition, regionSegment + domainSeparator);
```

Correctly inserts the region identifier after "collector." to produce hostnames like `collector.eu01.nr-data.net`.

#### ✅ MUST default to collector.newrelic.com if no match

**Spec Requirement:** "If the regex does not match, the hostname MUST default to collector.newrelic.com"

**Implementation:** [ConnectionInfo.cs:85](src/Agent/NewRelic/Agent/Core/DataTransport/ConnectionInfo.cs#L85)

```csharp
return defaultCollectorUrl;  // "collector.newrelic.com"
```

#### ✅ MUST NOT modify the license key

**Spec Requirement:** "do not modify the license key in any way (i.e. do not remove the region identifier before sending the key)"

**Status:** Compliant - The implementation only reads the license key for hostname determination. The original key is never modified and is sent unchanged in all requests.

### 2. Preconnect Request Behavior

#### ✅ MUST send preconnect to determined host

**Spec Requirement:** "Agents MUST send preconnect requests to the host as determined by the algorithms below"

**Implementation:** [ConnectionHandler.cs:171](src/Agent/NewRelic/Agent/Core/DataTransport/ConnectionHandler.cs#L171)

```csharp
_connectionInfo = new ConnectionInfo(_configuration);
```

Creates ConnectionInfo with only configuration (no redirect_host parameter), ensuring preconnect goes to the locally-configured, region-aware, or default host.

#### ✅ MUST NOT send preconnect to redirect_host

**Spec Requirement:** "Agents MUST NOT send preconnect requests to a redirect_host provided by a previous preconnect call"

**Implementation:** [ConnectionHandler.cs:171](src/Agent/NewRelic/Agent/Core/DataTransport/ConnectionHandler.cs#L171)

The ConnectionInfo constructor is called with a single parameter, which internally passes `null` for redirectHost, ensuring preconnect never uses a previous redirect value.

#### ✅ MUST use redirect_host for subsequent requests

**Spec Requirement:** The redirect_host from preconnect response should be used for all subsequent communication.

**Implementation:** [ConnectionHandler.cs:84-85](src/Agent/NewRelic/Agent/Core/DataTransport/ConnectionHandler.cs#L84-L85)

```csharp
var preconnectResult = SendPreconnectRequest();
_connectionInfo = new ConnectionInfo(_configuration, preconnectResult.RedirectHost);
```

After preconnect, a new ConnectionInfo is created with the redirect_host for all subsequent requests.

#### ✅ MUST NOT use default as fallback for region-specific failures

**Spec Requirement:** "The default collector hostname MUST NOT be used as a fallback if the region-specific hostname does not succeed"

**Implementation:** [ConnectionHandler.cs:116-121](src/Agent/NewRelic/Agent/Core/DataTransport/ConnectionHandler.cs#L116-L121)

```csharp
catch (Exception e)
{
    Disable();
    Log.Error(e, $"Unable to connect to the New Relic service at {_connectionInfo}");
    throw;
}
```

On connection failure, the agent disables itself and throws the exception. No fallback to default host occurs.

### 3. Local Configuration

#### ✅ Local configuration MUST take precedence

**Spec Requirement:** "local configuration for the collector hostname takes precedence over hostnames assembled from keys"

**Implementation:** [ConnectionInfo.cs:67-70](src/Agent/NewRelic/Agent/Core/DataTransport/ConnectionInfo.cs#L67-L70)

```csharp
if (!string.IsNullOrEmpty(configuration.CollectorHost))
{
    return configuration.CollectorHost;
}
```

Checks for local configuration first, before attempting license key parsing. Precedence order: local config → region-aware → default.

### 4. Validation

#### ✅ SHOULD run cross-agent tests

**Spec Requirement:** "Agents SHOULD pull in and run the collector hostname cross-agent-tests"

**Implementation:** [CollectorHostNameTests.cs:77-112](tests/Agent/UnitTests/Core.UnitTest/CrossAgentTests/DataTransport/CollectorHostNameTests.cs#L77-L112)

Comprehensive test suite loads test cases from `collector_hostname.json` and validates hostname resolution for various configuration combinations.

#### ✅ SHOULD NOT validate license key format

**Spec Requirement:** "agents SHOULD NOT check license keys for length or any other characteristic"

**Status:** Compliant - No validation of license key length or format exists. The code only checks if the key is non-null and whether the regex matches.


## Implementation Index

### Core Files

- **[ConnectionInfo.cs](src/Agent/NewRelic/Agent/Core/DataTransport/ConnectionInfo.cs)**
  - Line 37: Regex pattern definition
  - Lines 60-86: GetCollectorHost method (license key parsing and hostname construction)
  - Lines 67-70: Local configuration precedence
  - Lines 74-82: License key parsing and region extraction
  - Lines 39-58: Constructor with optional redirectHost parameter

- **[ConnectionHandler.cs](src/Agent/NewRelic/Agent/Core/DataTransport/ConnectionHandler.cs)**
  - Lines 74-122: Connect method (preconnect orchestration)
  - Lines 169-185: SendPreconnectRequest method
  - Line 171: ConnectionInfo creation for preconnect (no redirect host)
  - Lines 84-85: Redirect host usage for subsequent requests

- **[PreconnectResult.cs](src/Agent/NewRelic/Agent/Core/DataTransport/PreconnectResult.cs)**
  - Lines 11-12: RedirectHost property with JSON mapping

### Test Files

- **[CollectorHostNameTests.cs](tests/Agent/UnitTests/Core.UnitTest/CrossAgentTests/DataTransport/CollectorHostNameTests.cs)**
  - Lines 77-112: Cross-agent test method
  - Lines 114-131: Test data loader

- **[collector_hostname.json](tests/Agent/UnitTests/Core.UnitTest/CrossAgentTests/DataTransport/collector_hostname.json)**
  - Cross-agent test data


## Gap Analysis

### Critical Gaps (MUST requirements)

None identified. All MUST requirements are fully implemented.

### Important Gaps (SHOULD requirements)

None identified. All SHOULD recommendations are followed.

### Optional Gaps (MAY requirements)

Not applicable - specification contains no MAY requirements.


## Recommendations

### No Action Required

The .NET agent's Preconnect implementation is fully compliant with all specification requirements. No changes or enhancements are needed for compliance.

### Optional Enhancements (Out of Scope)

The following are not required by the spec but could provide operational benefits:
- Add debug logging for hostname resolution decisions
- Add supportability metrics for region-aware hostname usage

These are purely optional and not related to specification compliance.


## Compliance Summary Matrix

| Spec Requirement | Status | Priority | Implementation |
|-----------------|--------|----------|----------------|
| **License Key Parsing** |
| Use regex pattern ^.+?x | ✅ Compliant | MUST | ConnectionInfo.cs:37 |
| Strip trailing 'x' characters | ✅ Compliant | MUST | ConnectionInfo.cs:78 |
| Insert region between collector. and .nr-data.net | ✅ Compliant | MUST | ConnectionInfo.cs:79-80 |
| Default to collector.newrelic.com if no match | ✅ Compliant | MUST | ConnectionInfo.cs:85 |
| Do not modify license key | ✅ Compliant | MUST | Read-only usage |
| **Preconnect Behavior** |
| Send preconnect to determined host | ✅ Compliant | MUST | ConnectionHandler.cs:171 |
| Never send preconnect to redirect_host | ✅ Compliant | MUST | ConnectionHandler.cs:171 |
| Use redirect_host for subsequent requests | ✅ Compliant | MUST | ConnectionHandler.cs:85 |
| No fallback to default for region failures | ✅ Compliant | MUST | ConnectionHandler.cs:118 |
| **Local Configuration** |
| Local config takes precedence | ✅ Compliant | MUST | ConnectionInfo.cs:67-70 |
| **Validation** |
| Run cross-agent hostname tests | ✅ Compliant | SHOULD | CollectorHostNameTests.cs |
| Do not validate key length/format | ✅ Compliant | SHOULD | No validation code |

### Overall Compliance

- **MUST requirements:** 100% (10 of 10 compliant)
- **SHOULD requirements:** 100% (2 of 2 compliant)
- **Total compliance:** 100% ✅

---

*Report generated: 2026-01-26*
*Specification: Preconnect (01/2018)*
*Agent: .NET*
*Version: main branch*

