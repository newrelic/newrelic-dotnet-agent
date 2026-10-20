#!/usr/bin/env bash
# Tests for check-namespace-eligible.sh. Run: bash build/Scripts/check-namespace-eligible.tests.sh
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
SUT="$HERE/check-namespace-eligible.sh"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
fails=0

run() { # run <exe> -- sets $out to the output file and returns the exit code
  GITHUB_OUTPUT="$TMP/gh_output" bash "$SUT" "Platform=WindowsOnly" "Ns.Target" "$1" >"$TMP/out" 2>&1
}

expect() { # expect <label> <wanted-exit> <exe> [wanted-has_tests]
  local label="$1" want="$2" exe="$3" want_flag="${4:-}" got
  : > "$TMP/gh_output"
  run "$exe"
  got=$?
  if [ "$got" -ne "$want" ]; then
    echo "FAIL $label: wanted exit $want, got $got"
    sed 's/^/    /' "$TMP/out"
    fails=$((fails + 1))
    return
  fi
  if [ -n "$want_flag" ] && ! grep -qx "has_tests=$want_flag" "$TMP/gh_output"; then
    echo "FAIL $label: wanted has_tests=$want_flag in GITHUB_OUTPUT, got: $(tr '\n' ' ' < "$TMP/gh_output")"
    fails=$((fails + 1))
    return
  fi
  echo "PASS $label"
}

# Writes a stub launcher. It dispatches on the presence of -trait- anywhere in
# its arguments, because the two launches differ in argument order.
stub() { # stub <name> <matched-lines> <remaining-lines>
  cat > "$TMP/$1" <<STUB
#!/usr/bin/env bash
echo "xUnit.net v3 In-Process Runner v4.0.0 (stub)"
echo ""
excl=0
for a in "\$@"; do [ "\$a" = "-trait-" ] && excl=1; done
if [ "\$excl" = "0" ]; then printf '%s\n' $2; else printf '%s\n' $3; fi
STUB
  chmod +x "$TMP/$1"
}

stub mixed     "Ns.WinA Ns.WinB" "Ns.PortA Ns.PortB"
stub allwin    "Ns.WinA Ns.WinB" ""
stub dead      "Ns.WinA Ns.WinB" "Ns.WinA Ns.PortA"
stub nomatch   ""                "Ns.PortA Ns.PortB"

expect "namespace with portable classes runs"        0 "$TMP/mixed"   true
expect "namespace with no portable class skips"      0 "$TMP/allwin"  false
expect "dead exclusion filter fails"                 1 "$TMP/dead"
expect "trait absent from the assembly fails"        1 "$TMP/nomatch"
expect "missing launcher fails"                      1 "$TMP/absent"

# A launcher that crashes must be reported as a launch failure, never as a
# missing trait and never as an empty namespace.
cat > "$TMP/crashes" <<'STUB'
#!/usr/bin/env bash
echo "Unhandled exception: could not load file or assembly."
exit 1
STUB
chmod +x "$TMP/crashes"

: > "$TMP/gh_output"
run "$TMP/crashes"
got=$?
if [ "$got" -eq 1 ] && grep -q "Test launcher exited" "$TMP/out" && [ ! -s "$TMP/gh_output" ]; then
  echo "PASS a crashing launcher is a launch failure and sets no has_tests"
else
  echo "FAIL a crashing launcher is a launch failure and sets no has_tests: exit=$got output=$(tr '\n' ' ' < "$TMP/gh_output")"
  sed 's/^/    /' "$TMP/out"
  fails=$((fails + 1))
fi

# The namespace argument must reach the launcher, or the count is assembly-wide.
cat > "$TMP/echoargs" <<'STUB'
#!/usr/bin/env bash
excl=0
for a in "$@"; do [ "$a" = "-trait-" ] && excl=1; done
if [ "$excl" = "0" ]; then echo "Ns.WinA"; else echo "$@" >> "$TMP_ARGS"; echo "Ns.PortA"; fi
STUB
chmod +x "$TMP/echoargs"
TMP_ARGS="$TMP/args.out" : > "$TMP/args.out"
: > "$TMP/gh_output"
TMP_ARGS="$TMP/args.out" GITHUB_OUTPUT="$TMP/gh_output" \
  bash "$SUT" "Platform=WindowsOnly" "Ns.Target" "$TMP/echoargs" >"$TMP/out" 2>&1
if grep -q -- "-namespace Ns.Target" "$TMP/args.out"; then
  echo "PASS the namespace is passed to the exclusion launch"
else
  echo "FAIL the namespace is passed to the exclusion launch: $(cat "$TMP/args.out")"
  fails=$((fails + 1))
fi

echo "----"
[ "$fails" -eq 0 ] && { echo "all checks passed"; exit 0; }
echo "$fails check(s) failed"; exit 1
