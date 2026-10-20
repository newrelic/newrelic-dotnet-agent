#!/usr/bin/env bash
# Decides whether one namespace has a test class left after an exclusion trait
# filter, and proves the filter is live. Controls come from the assembly, so
# there are no class names or counts to keep in sync.
# Usage: check-namespace-eligible.sh <name=value> <namespace> <command> [args...]
set -uo pipefail

filter="${1:?trait filter as name=value required}"
namespace="${2:?fully qualified namespace required}"
shift 2
if [ "$#" -eq 0 ]; then
  echo "::error::No launch command given. Usage: check-namespace-eligible.sh <name=value> <namespace> <command> [args...]"
  exit 1
fi
cmd=("$@")
exe="${cmd[$(( $# - 1 ))]}"

if [ ! -f "$exe" ]; then
  echo "::error::No file at $exe to launch."
  exit 1
fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

launch() { # launch <outfile> [extra args...] -- exits the script on a non-zero launcher exit
  local out="$1"
  shift
  "${cmd[@]}" -list classes "$@" >"$out" 2>&1
  local rc=$?
  if [ "$rc" -ne 0 ]; then
    echo "::error::Test launcher exited $rc: ${cmd[*]} -list classes $*"
    head -5 "$out" | sed 's/^/    /'
    exit 1
  fi
}

classes() { # classes <outfile> -- sorted class names found in it
  grep -E '^[A-Za-z_][A-Za-z0-9_.]*\.[A-Za-z0-9_]+$' "$1" | sort
}

emit() { # emit <true|false>
  if [ -n "${GITHUB_OUTPUT:-}" ]; then
    echo "has_tests=$1" >> "$GITHUB_OUTPUT" || { echo "::error::Could not write to GITHUB_OUTPUT"; exit 1; }
  fi
}

launch "$tmp/matched.out" -trait "$filter"
launch "$tmp/remaining.out" -namespace "$namespace" -trait- "$filter"
matched="$(classes "$tmp/matched.out")"
remaining="$(classes "$tmp/remaining.out")"

if [ -z "$matched" ]; then
  echo "::error::No class in this assembly matches $filter. Either the trait is absent (a stale binary) or the filter is misspelled. An exclusion run cannot be trusted without it."
  exit 1
fi

leaked="$(comm -12 <(echo "$matched") <(echo "$remaining"))"
if [ -n "$leaked" ]; then
  echo "::error::-trait- $filter failed to exclude $(echo "$leaked" | wc -l) class(es) that -trait $filter matched. The exclusion filter is dead and this run would execute tests that cannot run on this target platform."
  echo "$leaked" | head -5 | sed 's/^/    /'
  exit 1
fi

count="$(echo "$remaining" | grep -c .)"
if [ "$count" -eq 0 ]; then
  echo "No class in $namespace survives -trait- $filter. Nothing to run on this target platform."
  emit false
  exit 0
fi

echo "$count class(es) in $namespace survive -trait- $filter."
emit true
exit 0
