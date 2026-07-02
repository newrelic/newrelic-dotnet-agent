#!/usr/bin/env bash
# Tests for combine-payload-logs.sh: BOM handling, cross-namespace merge, schema
# consistency (grandTotal == sum(byType) == sum(details)), and the empty case.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$SCRIPT_DIR/../combine-payload-logs.sh"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }

IN="$tmp/in"
mkdir -p "$IN"
# ns A carries a UTF-8 BOM (as the PowerShell extractor would write); both
# namespaces share class "Shared" to exercise cross-namespace summing.
printf '\xEF\xBB\xBF{"AlphaTests":{"connect":100,"metric_data":200},"Shared":{"connect":10}}' > "$IN/a_payload_bytes.json"
printf '{"BetaTests":{"connect":50},"Shared":{"metric_data":4}}' > "$IN/b_payload_bytes.json"

OUT="$tmp/out.json"
bash "$SCRIPT" "$IN" "Integration Tests" "$OUT" >/dev/null 2>&1 || fail "combine failed"
jq -e . "$OUT" >/dev/null 2>&1 || fail "output is not valid JSON (BOM not stripped?)"

grand="$(jq -r '.grandTotalBytes' "$OUT")"
[ "$grand" = "364" ] || fail "expected grandTotalBytes 364, got $grand"        # 100+200+10 + 50+4
[ "$(jq -r '.byType.connect' "$OUT")" = "160" ] || fail "expected byType.connect 160"   # 100+10+50
[ "$(jq -r '.details[] | select(.className=="Shared") | .bytes' "$OUT")" = "14" ] || fail "expected Shared 14" # 10+4
[ "$(jq -r '.testType' "$OUT")" = "Integration Tests" ] || fail "wrong testType label"

sumBy="$(jq -r '[.byType[]]|add' "$OUT")"
sumDet="$(jq -r '[.details[].bytes]|add' "$OUT")"
{ [ "$grand" = "$sumBy" ] && [ "$grand" = "$sumDet" ]; } || fail "sums disagree: grand=$grand byType=$sumBy details=$sumDet"

# Empty input dir -> valid empty summary.
EMPTY="$tmp/empty"
mkdir -p "$EMPTY"
OUT2="$tmp/out2.json"
bash "$SCRIPT" "$EMPTY" "Unbounded Integration Tests" "$OUT2" >/dev/null 2>&1 || fail "empty combine failed"
[ "$(jq -r '.grandTotalBytes' "$OUT2")" = "0" ] || fail "expected grandTotalBytes 0 for empty input"
[ "$(jq -r '.details|length' "$OUT2")" = "0" ] || fail "expected empty details for empty input"

echo "PASS: all combine assertions passed"
