#!/usr/bin/env bash
# Tests for extract-payload-bytes.py: it must sum bytes per class/category from a
# TRX, read the executed count from ResultSummary/Counters, emit BOM-free JSON,
# and treat a missing TRX as empty rather than an error.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$SCRIPT_DIR/../extract-payload-bytes.py"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

PY=python3
command -v python3 >/dev/null 2>&1 || PY=python

fail() { echo "FAIL: $1" >&2; exit 1; }

# A TRX carries the VSTest namespace, and the payload lines arrive as test output
# text. Two AlphaTests/connect lines prove accumulation.
cat > "$tmp/results.trx" <<'TRX'
<?xml version="1.0" encoding="UTF-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
    <UnitTestResult testName="One">
      <Output>
        <TextMessages>
          <Message>AlphaTests: connect: 100 bytes
AlphaTests: metric_data: 250 bytes
AlphaTests: connect: 50 bytes
not a payload line</Message>
        </TextMessages>
      </Output>
    </UnitTestResult>
    <UnitTestResult testName="Two">
      <Output>
        <TextMessages>
          <Message>BetaTests: span_event_data: 7 bytes</Message>
        </TextMessages>
      </Output>
    </UnitTestResult>
  </Results>
  <ResultSummary outcome="Completed">
    <Counters total="5" executed="4" passed="4" failed="0" />
  </ResultSummary>
</TestRun>
TRX

"$PY" "$SCRIPT" "$tmp/results.trx" "$tmp/payload.json" "$tmp/count.json" >/dev/null \
  || fail "expected success on a valid TRX"

[ "$(jq -r '.AlphaTests.connect' "$tmp/payload.json")" = "150" ] \
  || fail "expected AlphaTests/connect to sum to 150"
[ "$(jq -r '.AlphaTests.metric_data' "$tmp/payload.json")" = "250" ] \
  || fail "expected AlphaTests/metric_data 250"
[ "$(jq -r '.BetaTests.span_event_data' "$tmp/payload.json")" = "7" ] \
  || fail "expected BetaTests/span_event_data 7"
[ "$(jq -r '.executedTests' "$tmp/count.json")" = "4" ] \
  || fail "expected executedTests 4 from ResultSummary/Counters"

# combine-payload-logs.sh strips a BOM defensively; this writer must not emit one.
if head -c 3 "$tmp/payload.json" | grep -q $'\xEF\xBB\xBF'; then
  fail "payload JSON must not start with a BOM"
fi

# A namespace that produced no TRX must yield empty results and exit 0.
"$PY" "$SCRIPT" "$tmp/absent.trx" "$tmp/empty.json" "$tmp/empty-count.json" >/dev/null \
  || fail "expected exit 0 when the TRX is missing"
[ "$(cat "$tmp/empty.json")" = "{}" ] || fail "expected empty payload object"
[ "$(jq -r '.executedTests' "$tmp/empty-count.json")" = "0" ] \
  || fail "expected executedTests 0 when the TRX is missing"

# A TRX with no Counters executed attribute must report 0, not crash.
cat > "$tmp/nocounters.trx" <<'TRX'
<?xml version="1.0" encoding="UTF-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results />
</TestRun>
TRX
"$PY" "$SCRIPT" "$tmp/nocounters.trx" "$tmp/nc.json" "$tmp/nc-count.json" >/dev/null \
  || fail "expected exit 0 on a TRX without Counters"
[ "$(jq -r '.executedTests' "$tmp/nc-count.json")" = "0" ] \
  || fail "expected executedTests 0 without Counters"

# Wrong argument count is a usage error.
if "$PY" "$SCRIPT" "$tmp/results.trx" >/dev/null 2>&1; then
  fail "expected usage failure with too few arguments"
fi

echo "PASS: all extract-payload-bytes assertions passed"
