#!/usr/bin/env bash
# Fails a test lane on the two failure modes that otherwise pass silently:
#   1. zero tests matched the lane's filter (TRX Counters/@total == 0)
#   2. the pre-built publish output was missing, so apps published at test time
# Usage: check-test-run.sh <trx-path> <runner-log-path>
set -uo pipefail

trx="${1:?trx path required}"
log="${2:?runner log path required}"
rc=0

if [ ! -f "$trx" ]; then
  echo "::error::No TRX at $trx. The runner did not produce results; treat this as a failed lane."
  exit 1
fi

total="$(grep -o '<Counters[^>]*total="[0-9]*"' "$trx" | grep -o 'total="[0-9]*"' | head -1 | tr -dc '0-9')"
if [ -z "$total" ]; then
  echo "::error::Could not read Counters/@total from $trx. The TRX schema changed; fix this check."
  rc=1
elif [ "$total" -eq 0 ]; then
  echo "::error::Zero tests matched this lane's filter. A shard with no tests for its lane is a misclassification, not a pass. Check the shard's Runtime traits and its entry in test_selection.yml."
  rc=1
fi

if [ ! -f "$log" ]; then
  echo "::error::No runner log at $log. The prebuilt-publish check cannot run without it; check the artifact path passed to this script."
  rc=1
elif grep -q 'Pre-built publish output not found' "$log"; then
  echo "::error::A test application published at test time instead of using pre-built output. The build job did not publish for this runtime, so the lane spent the minutes pre-publishing was meant to save."
  grep -n 'Pre-built publish output not found' "$log" | head -5
  rc=1
fi

exit "$rc"
