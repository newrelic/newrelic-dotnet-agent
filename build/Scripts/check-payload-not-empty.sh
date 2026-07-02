#!/usr/bin/env bash
# Guard: fail loudly if the consolidated payload summary reports (near-)zero total bytes.
#
# When payload aggregation runs, the test suites always send a large amount of
# data (every test app at least does a connect handshake), so an overall total
# of zero is never legitimate - it means the TRX / agent-log parsing broke
# (e.g. a log-line format change) somewhere upstream. Without this guard the
# pipeline would silently report zeros to New Relic and the dashboard would
# flatline with no failure. This step turns that silent failure into a loud one.
#
# Usage:
#   check-payload-not-empty.sh [SUMMARY_FILE]
#   SUMMARY_FILE            path to final_payload_summary.json
#                           (arg 1, or PAYLOAD_SUMMARY_FILE env var)
#   MIN_EXPECTED_PAYLOAD_BYTES  floor; fail if total <= this (default 0).
#                               Raise it later to catch partial breakage too.
#
# Exit codes:
#   0  total is above the floor
#   1  summary file missing/unparseable, or total <= floor
set -euo pipefail

SUMMARY_FILE="${1:-${PAYLOAD_SUMMARY_FILE:-}}"
FLOOR="${MIN_EXPECTED_PAYLOAD_BYTES:-0}"

if [ -z "$SUMMARY_FILE" ] || [ ! -f "$SUMMARY_FILE" ]; then
  echo "ERROR: payload summary file not found (got '$SUMMARY_FILE')." >&2
  exit 1
fi

total="$(jq -r '(.bytes // 0) | floor' "$SUMMARY_FILE" 2>/dev/null || true)"
case "$total" in
  '' | *[!0-9]*)
    echo "ERROR: could not read a numeric total from '$SUMMARY_FILE' (got '$total')." >&2
    exit 1
    ;;
esac

echo "Overall payload bytes: $total (floor: $FLOOR)"
if [ "$total" -le "$FLOOR" ]; then
  echo "ERROR: payload total ($total) is at or below the floor ($FLOOR)." >&2
  echo "This almost certainly means the TRX / agent-log parsing broke (e.g. a log-line" >&2
  echo "format change), not a real zero. Failing so it is investigated instead of" >&2
  echo "silently reporting zeros to New Relic." >&2
  exit 1
fi

echo "Payload guard passed."
