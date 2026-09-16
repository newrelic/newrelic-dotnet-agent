#!/usr/bin/env bash
# Proves an exclusion trait filter is live before a lane runs. check-test-run.sh
# cannot see a dead exclusion filter: a broken one excludes nothing, so the lane
# runs everything and still passes on a Windows runner. Controls come from the
# assembly itself, so there are no class names or counts to keep in sync.
# Usage: check-trait-filter.sh <name=value> <command> [args...]
set -uo pipefail

filter="${1:?trait filter as name=value required}"
shift
if [ "$#" -eq 0 ]; then
  echo "::error::No launch command given. Usage: check-trait-filter.sh <name=value> <command> [args...]"
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

launch() { # launch <-trait|-trait-> <outfile> -- exits the script on a non-zero launcher exit
  "${cmd[@]}" -list classes "$1" "$filter" >"$2" 2>&1
  local rc=$?
  if [ "$rc" -ne 0 ]; then
    echo "::error::Test launcher exited $rc: ${cmd[*]} -list classes $1 $filter"
    head -5 "$2" | sed 's/^/    /'
    exit 1
  fi
}

classes() { # classes <outfile> -- sorted class names found in it
  grep -E '^[A-Za-z_][A-Za-z0-9_.]*\.[A-Za-z0-9_]+$' "$1" | sort
}

launch -trait "$tmp/matched.out"
launch -trait- "$tmp/remaining.out"
matched="$(classes "$tmp/matched.out")"
remaining="$(classes "$tmp/remaining.out")"

if [ -z "$matched" ]; then
  echo "::error::No class matches $filter. Either the trait is absent from this assembly (a stale binary, or the trait tool was never run) or the filter is misspelled. An exclusion lane cannot be trusted without it."
  exit 1
fi

if [ -z "$remaining" ]; then
  echo "::error::Excluding $filter leaves no class at all. Every class carries the trait, which is not a lane."
  exit 1
fi

leaked="$(comm -12 <(echo "$matched") <(echo "$remaining"))"
if [ -n "$leaked" ]; then
  echo "::error::-trait- $filter failed to exclude $(echo "$leaked" | wc -l) class(es) that -trait $filter matched. The exclusion filter is dead and this lane would run Windows-only tests."
  echo "$leaked" | head -5 | sed 's/^/    /'
  exit 1
fi

echo "Exclusion filter live: $(echo "$matched" | wc -l) class(es) match $filter, all excluded; $(echo "$remaining" | wc -l) remain."
exit 0
