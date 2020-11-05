#!/bin/bash

: "${SHELLCHECK:=shellcheck}"

if ! command -v "$SHELLCHECK" > /dev/null; then
  printf 'shellcheck is required to run this script\n' >&2
  printf 'see: https://github.com/koalaman/shellcheck\n' >&2

  if [[ "$(uname -s)" = Darwin ]]; then
    printf \\n >&2
    printf 'shellcheck is available from Homebrew: brew install shellcheck\n' >&2
  fi

  exit 1
fi

"$SHELLCHECK" -s bash -x ./*.sh scripts/*.sh
