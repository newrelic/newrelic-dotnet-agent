#!/usr/bin/env bash
# Tests for check-test-run.sh. Run: bash build/Scripts/check-test-run.tests.sh
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
SUT="$HERE/check-test-run.sh"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
fails=0

expect() { # expect <label> <wanted-exit> <trx> <log>
  local label="$1" want="$2" trx="$3" log="$4" got
  bash "$SUT" "$trx" "$log" >"$TMP/out" 2>&1
  got=$?
  if [ "$got" -ne "$want" ]; then
    echo "FAIL $label: wanted exit $want, got $got"
    sed 's/^/    /' "$TMP/out"
    fails=$((fails + 1))
  else
    echo "PASS $label"
  fi
}

counters() { # counters <total> > file
  printf '<?xml version="1.0"?><TestRun><ResultSummary><Counters total="%s" executed="%s" passed="%s" failed="0" /></ResultSummary></TestRun>\n' "$1" "$1" "$1"
}

counters 6 > "$TMP/ok.trx"
counters 0 > "$TMP/zero.trx"
echo '[RemoteService]: Copied pre-built publish output to /tmp/x.' > "$TMP/ok.log"
echo '[RemoteService]: Pre-built publish output not found at /tmp/x, falling back to dotnet publish.' > "$TMP/fallback.log"
: > "$TMP/empty.log"

expect "healthy run passes"                    0 "$TMP/ok.trx"   "$TMP/ok.log"
expect "zero total fails"                      1 "$TMP/zero.trx" "$TMP/ok.log"
expect "prebuilt fallback fails"               1 "$TMP/ok.trx"   "$TMP/fallback.log"
expect "missing trx fails"                     1 "$TMP/absent.trx" "$TMP/ok.log"
expect "missing log fails"                     1 "$TMP/ok.trx"  "$TMP/absent.log"
expect "no prebuilt evidence either way passes" 0 "$TMP/ok.trx"  "$TMP/empty.log"

echo "----"
[ "$fails" -eq 0 ] && { echo "all checks passed"; exit 0; }
echo "$fails check(s) failed"; exit 1
