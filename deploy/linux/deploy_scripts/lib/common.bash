#!/bin/bash
# common.bash - utility functions

#
# Print usage and exit.
#
#   $* - optional, an error message to print
#
usage() {
  local exitstatus=0

  if [[ $# -gt 0 ]]; then
    printf '%s: %s\n\n' "$(basename "$0")" "$*" >&2
    exitstatus=2
  fi

  grep '^###' "$0" | cut -c '5-' >&2
  exit $exitstatus
}

#
# Print a message and exit with an error.
#
die() {
  if [[ $# -gt 0 ]]; then
    printf '%s: %s\n' "$(basename "$0")" "$*" >&2
  fi
  exit 1
}

#
# Wraps printf, but only prints if verbose output is enabled.
#
vprintf() {
  if [[ $VERBOSE = yes ]]; then
    printf -- "$@"
  fi
}

#
# Prompt the user with a yes/no question and return zero if the
# user responds yes; otherwise, returns non-zero.
#
ask() {
  local REPLY

  printf \\n
  read -e -r -p "$1 (y/N) " && [[ $REPLY = 'y' || $REPLY = 'Y' ]] || return 1
}

#
# Set a variable to the value of a long option. This
# depends on OPTARG and OPTIND being in scope and set as
# part of a getopts loop.
#
#   $1 - name of the variable to set.
#   $* - the program arguments
#
optparse() {
  local var=$1; shift

  if [[ $OPTARG = *=* ]]; then
    eval $var='${OPTARG#*=}'
  elif [[ $# -lt $OPTIND ]]; then
    usage "option requires an argument -- $OPTARG"
  else
    eval $var='${!OPTIND}'
    OPTIND=$((OPTIND + 1))
  fi
}

#
# Validate that the product is one of the expected values.
#
validate_product() {
  case $1 in
    php_agent|server_monitor|dotnet_agent) ;;
    *) die 'product must be php_agent, dotnet_agent, or server_monitor' ;;
  esac
}
