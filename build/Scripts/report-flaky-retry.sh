#!/usr/bin/env bash
# Report a single flaky-retry occurrence to the New Relic Event API (staging by
# default) as a DotnetCiTestRetry custom event.
#
# Called INLINE from the retry-once loops in all_solutions.yml (integration +
# unbounded, via `bash`) and linux_container_tests.yml (container). Emit exactly
# one event when a test group FAILED its first attempt but PASSED on the single
# retry -- the transient-flake signal. Fail-both is already a red build and is
# not reported here.
#
# This runs on EVERY CI run (pull requests included). It is best-effort
# telemetry: a delivery failure must NEVER fail the CI job (the tests passed),
# so every path exits 0 and errors are swallowed with a warning.
#
# Env vars:
#   NR_TEST_SECRETS   Raw TEST_SECRETS JSON, may carry a UTF-8 BOM. License key
#                     is read from .IntegrationTestConfiguration.DefaultSetting.LicenseKey
#                     and the account id from .IntegrationTestConfiguration.DefaultSetting.NewRelicAccountId
#   RETRY_TEST_TYPE   testType attribute (e.g. integrationTests / unboundedTests / containerTests)
#   RETRY_TEST_GROUP  testGroup attribute (namespace, or "<arch>/<distro>" for container)
#   CI_BRANCH CI_COMMIT CI_RUN_ID CI_RUN_URL CI_TRIGGER   Optional metadata attributes
#   NR_INSIGHTS_HOST  Event API host (default staging-insights-collector.newrelic.com)
#   DRY_RUN           "1"/"true" (or first arg --dry-run) => print body, do not POST
set -uo pipefail

DRY_RUN="${DRY_RUN:-}"
if [ "${1:-}" = "--dry-run" ]; then
  DRY_RUN=1
fi
is_dry_run() { [ "$DRY_RUN" = "1" ] || [ "$DRY_RUN" = "true" ]; }

TEST_TYPE="${RETRY_TEST_TYPE:-unknown}"
TEST_GROUP="${RETRY_TEST_GROUP:-unknown}"
INSIGHTS_HOST="${NR_INSIGHTS_HOST:-staging-insights-collector.newrelic.com}"

# Build the Event API request body: a single-element array with one custom event.
BODY="$(jq -cn \
  --arg tt "$TEST_TYPE" \
  --arg tg "$TEST_GROUP" \
  --arg branch "${CI_BRANCH:-unknown}" \
  --arg commit "${CI_COMMIT:-unknown}" \
  --arg runId "${CI_RUN_ID:-unknown}" \
  --arg runUrl "${CI_RUN_URL:-unknown}" \
  --arg trigger "${CI_TRIGGER:-unknown}" '
  [ { eventType: "DotnetCiTestRetry",
      testType: $tt,
      testGroup: $tg,
      source: "dotnet-agent-ci",
      "ci.branch": $branch,
      "ci.commit": $commit,
      "ci.runId": $runId,
      "ci.runUrl": $runUrl,
      "ci.trigger": $trigger } ]')"

echo "Flaky-retry event for testType=$TEST_TYPE testGroup=$TEST_GROUP" >&2

if is_dry_run; then
  echo "$BODY"
  exit 0
fi

# Parse license key + account id from TEST_SECRETS (nested JSON); strip a UTF-8 BOM.
LICENSE_KEY=""
ACCOUNT_ID=""
if [ -n "${NR_TEST_SECRETS:-}" ]; then
  CLEAN_SECRETS="$(printf '%s' "$NR_TEST_SECRETS" | sed '1s/^\xEF\xBB\xBF//')"
  LICENSE_KEY="$(printf '%s' "$CLEAN_SECRETS" | jq -r '.IntegrationTestConfiguration.DefaultSetting.LicenseKey // empty' 2>/dev/null)"
  ACCOUNT_ID="$(printf '%s' "$CLEAN_SECRETS" | jq -r '.IntegrationTestConfiguration.DefaultSetting.NewRelicAccountId // empty' 2>/dev/null)"
fi

# Mask the extracted license key in CI logs. GitHub Actions only auto-masks the
# exact TEST_SECRETS blob, not this jq-extracted substring. No-op outside Actions.
[ -n "$LICENSE_KEY" ] && echo "::add-mask::$LICENSE_KEY"

if [ -z "$LICENSE_KEY" ] || [ -z "$ACCOUNT_ID" ]; then
  echo "WARNING: no New Relic license key / account id available; skipping flaky-retry event." >&2
  exit 0
fi

EVENT_URL="https://${INSIGHTS_HOST}/v1/accounts/${ACCOUNT_ID}/events"
RESP_FILE="$(mktemp)"
trap 'rm -f "$RESP_FILE"' EXIT

HTTP_CODE="$(curl -s --connect-timeout 10 --max-time 30 -o "$RESP_FILE" -w '%{http_code}' \
  -H "Content-Type: application/json" \
  -H "Api-Key: $LICENSE_KEY" \
  -X POST "$EVENT_URL" \
  --data-binary "$BODY" 2>/dev/null || echo "000")"

if [ "$HTTP_CODE" = "200" ]; then
  echo "New Relic Event API accepted the DotnetCiTestRetry event (HTTP 200)." >&2
else
  echo "WARNING: New Relic Event API returned HTTP $HTTP_CODE for flaky-retry event (ignored)." >&2
fi

# Best-effort telemetry: never fail the job.
exit 0
