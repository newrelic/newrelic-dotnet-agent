#!/usr/bin/env bash
# Report aggregated CI payload sizes to the New Relic Metric API.
# Reads final_payload_summary.json (produced by all_solutions.yml), transforms
# it into Metric API JSON, and POSTs it to New Relic (staging by default).
#
# Env vars:
#   PAYLOAD_SUMMARY_FILE  Path to final_payload_summary.json (required)
#   NR_METRIC_API_URL     Metric API endpoint (required unless dry-run)
#   NR_TEST_SECRETS       Raw TEST_SECRETS JSON, may carry a UTF-8 BOM
#                         (required unless dry-run). License key is read from
#                         .IntegrationTestConfiguration.DefaultSetting.LicenseKey
#   CI_BRANCH CI_COMMIT CI_RUN_ID CI_RUN_URL   Optional metadata attributes
#   DRY_RUN               "1"/"true" => print request body to stdout, do not POST
#
# Exit codes:
#   0  success (HTTP 202), dry-run, or intentional skip (no file / key / url)
#   1  POST attempted but response was not HTTP 202
set -euo pipefail

DRY_RUN="${DRY_RUN:-}"
if [ "${1:-}" = "--dry-run" ]; then
  DRY_RUN=1
fi
is_dry_run() { [ "$DRY_RUN" = "1" ] || [ "$DRY_RUN" = "true" ]; }

SUMMARY_FILE="${PAYLOAD_SUMMARY_FILE:-}"
if [ -z "$SUMMARY_FILE" ] || [ ! -f "$SUMMARY_FILE" ]; then
  echo "WARNING: payload summary file not found (PAYLOAD_SUMMARY_FILE='$SUMMARY_FILE'); skipping." >&2
  exit 0
fi

# Build the Metric API request body from the summary artifact.
# - per-class gauge (deduped by testType+className)
# - per-type total gauge, plus an "all" rollup gauge
build_body() {
  local ts_ms="$1"
  jq -c \
    --arg ts "$ts_ms" \
    --arg branch "${CI_BRANCH:-unknown}" \
    --arg commit "${CI_COMMIT:-unknown}" \
    --arg runId "${CI_RUN_ID:-unknown}" \
    --arg runUrl "${CI_RUN_URL:-unknown}" '
    ([ to_entries[] | select(.value|type=="object" and has("details")) ]) as $types
    | [ {
        common: { timestamp: ($ts|tonumber),
                  attributes: { source:"dotnet-agent-ci", "ci.branch":$branch,
                                "ci.commit":$commit, "ci.runId":$runId, "ci.runUrl":$runUrl } },
        metrics: (
          [ $types[] | .key as $t | (.value.details // [])
            | group_by(.className)[]
            | {name:"newrelic.dotnet.ci.payload.bytes", type:"gauge",
               value:(map(.bytes)|add), attributes:{testType:$t, className:.[0].className}} ]
          + [ $types[] | {name:"newrelic.dotnet.ci.payload.total.bytes", type:"gauge",
               value:.value.bytes, attributes:{testType:.key}} ]
          + [ {name:"newrelic.dotnet.ci.payload.total.bytes", type:"gauge",
               value:(.bytes // 0), attributes:{testType:"all"}} ]
          + [ $types[] | .key as $t | (.value.byType // {}) | to_entries[]
            | {name:"newrelic.dotnet.ci.payload.by_type.bytes", type:"gauge",
               value:.value, attributes:{testType:$t, payloadType:.key}} ]
          + [ (.byType // {}) | to_entries[]
            | {name:"newrelic.dotnet.ci.payload.by_type.bytes", type:"gauge",
               value:.value, attributes:{testType:"all", payloadType:.key}} ]
          + [ $types[] | {name:"newrelic.dotnet.ci.tests.count", type:"gauge",
               value:(.value.executedTests // 0), attributes:{testType:.key}} ]
          + [ {name:"newrelic.dotnet.ci.tests.count", type:"gauge",
               value:(.executedTests // 0), attributes:{testType:"all"}} ]
        )
      } ]
  ' "$SUMMARY_FILE"
}

TS_MS="$(date +%s%3N)"
BODY_FILE="$(mktemp)"
RESP_FILE="$(mktemp)"
trap 'rm -f "$BODY_FILE" "$RESP_FILE"' EXIT

build_body "$TS_MS" > "$BODY_FILE"
METRIC_COUNT="$(jq '.[0].metrics | length' "$BODY_FILE")"
echo "Built $METRIC_COUNT metrics from $SUMMARY_FILE (timestamp ${TS_MS}ms)." >&2

if is_dry_run; then
  cat "$BODY_FILE"
  exit 0
fi

# Extract the license key (Api-Key) from TEST_SECRETS; strip a possible UTF-8 BOM.
LICENSE_KEY=""
if [ -n "${NR_TEST_SECRETS:-}" ]; then
  LICENSE_KEY="$(printf '%s' "$NR_TEST_SECRETS" | sed '1s/^\xEF\xBB\xBF//' \
    | jq -r '.IntegrationTestConfiguration.DefaultSetting.LicenseKey // empty')"
fi

# Mask the extracted license key in CI logs. GitHub Actions only auto-masks the
# exact TEST_SECRETS blob, not this jq-extracted substring. No-op outside Actions.
[ -n "$LICENSE_KEY" ] && echo "::add-mask::$LICENSE_KEY"

if [ -z "$LICENSE_KEY" ]; then
  echo "WARNING: no New Relic license key available; skipping." >&2
  exit 0
fi
if [ -z "${NR_METRIC_API_URL:-}" ]; then
  echo "WARNING: NR_METRIC_API_URL not set; skipping." >&2
  exit 0
fi

HTTP_CODE="$(curl -s --connect-timeout 10 --max-time 30 -o "$RESP_FILE" -w '%{http_code}' \
  -H "Content-Type: application/json" \
  -H "Api-Key: $LICENSE_KEY" \
  -X POST "$NR_METRIC_API_URL" \
  --data-binary @"$BODY_FILE")"

if [ "$HTTP_CODE" = "202" ]; then
  echo "New Relic Metric API accepted the payload (HTTP 202). requestId=$(jq -r '.requestId // "unknown"' "$RESP_FILE")" >&2
  exit 0
fi

echo "ERROR: New Relic Metric API returned HTTP $HTTP_CODE" >&2
echo "Response: $(cat "$RESP_FILE")" >&2
exit 1
