#!/usr/bin/env bash
# Combine per-namespace payload-bytes JSON files into one per-test-type summary.
#
# Each input file is nested {className: {category: bytes}} (produced by the
# "Extract payload bytes log from TRX" steps). This merges all of them for one
# test type and emits the combined schema consumed by the final-merge step:
#   { testType, generatedAt, grandTotalBytes, details: [{className, bytes}], byType: {category: bytes} }
#
# Single-sourced on purpose: all three aggregate jobs (integration, unbounded,
# container) call this, so the schema and merge logic live in exactly one place.
#
# Usage: combine-payload-logs.sh <IN_DIR> <LABEL> <OUT>
#   IN_DIR  directory of downloaded *_payload_bytes.json files
#   LABEL   testType label, e.g. "Integration Tests"
#   OUT     output path for the combined JSON
set -euo pipefail

IN_DIR="${1:?usage: combine-payload-logs.sh <IN_DIR> <LABEL> <OUT>}"
LABEL="${2:?usage: combine-payload-logs.sh <IN_DIR> <LABEL> <OUT>}"
OUT="${3:?usage: combine-payload-logs.sh <IN_DIR> <LABEL> <OUT>}"
NOW="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"

mkdir -p "$(dirname "$OUT")"

# Sum the executed-test counts from the sibling *_test_count.json files that ride
# in the same per-namespace artifacts. Each file is {"executedTests": N}; strip any
# UTF-8 BOM first (the PowerShell extractors write one; container files are BOM-free).
EXECUTED_TESTS=0
if [ -d "$IN_DIR" ] && find "$IN_DIR" -name '*_test_count.json' -type f | grep -q .; then
  find "$IN_DIR" -name '*_test_count.json' -type f -exec sed -i '1s/^\xEF\xBB\xBF//' {} +
  EXECUTED_TESTS="$(find "$IN_DIR" -name '*_test_count.json' -type f -print0 | xargs -0 cat \
    | jq -s '[.[].executedTests // 0] | add // 0')"
fi

if [ -d "$IN_DIR" ] && find "$IN_DIR" -name '*_payload_bytes.json' -type f | grep -q .; then
  # The PowerShell extractors write UTF-8 with a BOM; strip it so jq can parse
  # the concatenated stream. (Container files are already BOM-free; no-op there.)
  find "$IN_DIR" -name '*_payload_bytes.json' -type f -exec sed -i '1s/^\xEF\xBB\xBF//' {} +
  # `xargs -0 cat | jq -s` keeps it a single jq invocation even if the file list
  # would exceed ARG_MAX (a split xargs would otherwise emit concatenated JSON).
  find "$IN_DIR" -name '*_payload_bytes.json' -type f -print0 | xargs -0 cat \
    | jq -s --arg tt "$LABEL" --arg now "$NOW" --argjson exec "$EXECUTED_TESTS" '
      (reduce .[] as $ns ({};
         reduce ($ns|to_entries[]) as $c (.;
           reduce ($c.value|to_entries[]) as $cat (.;
             .[$c.key][$cat.key] = ((.[$c.key][$cat.key]//0) + $cat.value) )))) as $m
      | { testType: $tt, generatedAt: $now, executedTests: $exec,
          grandTotalBytes: ([$m[][]] | add // 0),
          details: [ $m | to_entries[] | {className:.key, bytes:([.value[]]|add // 0)} ],
          byType: (reduce ($m|to_entries[]) as $c ({};
                     reduce ($c.value|to_entries[]) as $cat (.;
                       .[$cat.key] = ((.[$cat.key]//0)+$cat.value)))) }' > "$OUT"
else
  jq -n --arg tt "$LABEL" --arg now "$NOW" --argjson exec "$EXECUTED_TESTS" \
    '{testType:$tt, generatedAt:$now, executedTests:$exec, grandTotalBytes:0, details:[], byType:{}}' > "$OUT"
fi

echo "Combined -> $OUT"
cat "$OUT"
