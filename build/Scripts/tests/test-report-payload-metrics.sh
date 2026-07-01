#!/usr/bin/env bash
# Local test for report-payload-metrics.sh: runs the transform in dry-run mode
# against a fixture and asserts the generated Metric API body is correct.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$SCRIPT_DIR/../report-payload-metrics.sh"
FIXTURE="$SCRIPT_DIR/fixtures/sample-payload-summary.json"

fail() { echo "FAIL: $1" >&2; exit 1; }

# In dry-run mode, stdout is exactly the Metric API body (one line of JSON).
OUT="$(PAYLOAD_SUMMARY_FILE="$FIXTURE" DRY_RUN=1 bash "$SCRIPT")"

echo "$OUT" | jq -e . >/dev/null 2>&1 || fail "dry-run output is not valid JSON"

COUNT="$(echo "$OUT" | jq '.[0].metrics | length')"
[ "$COUNT" = "6" ] || fail "expected 6 metrics, got $COUNT"

ALPHA="$(echo "$OUT" | jq -r '.[0].metrics[]
  | select(.name=="newrelic.dotnet.ci.payload.bytes" and .attributes.className=="AlphaTests")
  | .value')"
[ "$ALPHA" = "250" ] || fail "expected AlphaTests deduped to 250, got $ALPHA"

INT_TOTAL="$(echo "$OUT" | jq -r '.[0].metrics[]
  | select(.name=="newrelic.dotnet.ci.payload.total.bytes" and .attributes.testType=="integrationTests")
  | .value')"
[ "$INT_TOTAL" = "300" ] || fail "expected integrationTests total 300, got $INT_TOTAL"

ALL="$(echo "$OUT" | jq -r '.[0].metrics[]
  | select(.name=="newrelic.dotnet.ci.payload.total.bytes" and .attributes.testType=="all")
  | .value')"
[ "$ALL" = "325" ] || fail "expected overall total 325, got $ALL"

echo "PASS: all assertions passed ($COUNT metrics)"
