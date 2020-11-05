#!/bin/bash
# repoman-demote.bash - remove build artifacts from a repository

### Usage: repoman-demote.bash --product=<name> --destination=<name>
###                            --prefix=<dir> [--skip-apt] [--skip-yum]
###                            [--skip-other] [--color] [--no-legend]
###                            [-n | --dry-run] [-v | --verbose] [--]
###                            <file>...
###        repoman-demote.bash [--help]
###
### Arguments:
###    <file>...              files to demote
###
### Options:
###    --product=<product>    product (php_agent or server_monitor)
###    --destination=<name>   where the files should be demoted
###    --prefix=<dir>         path to the directory containing the repository
###    --skip-apt             do not perform APT repository updates
###    --skip-yum             do not perform YUM repository updates
###    --skip-other           do not perform product repository updates
###    --color                colorize the output
###    --no-legend            suppress output of legend
###    -v, --verbose          cause demote to be verbose
###    -n, --dry-run          don't actually demote the files, just show the
###                           steps that would be performed
###    --help                 print this message and exit
###

set -e

export ACTION

if [[ ! -d $DEPLOY_HOME ]]; then
  printf '%s: DEPLOY_HOME is not defined\n' "$(basename "$0")" >&2
  exit 1
fi

# shellcheck source=lib/common.bash
source "$DEPLOY_HOME/lib/common.bash"

declare DRYRUN=no VERBOSE=no COLORIZE=no LEGEND=yes
declare PRODUCT DESTINATION PREFIX SKIP REBUILD

[[ $# -gt 0 ]] || usage

while getopts ':nv-:' OPTNAME; do
  case $OPTNAME in
    n) DRYRUN=yes ;;
    v) VERBOSE=yes ;;
    -)
      case $OPTARG in
	'help') usage ;;
	color) COLORIZE=yes ;;
	dry-run) DRYRUN=yes ;;
	verbose) VERBOSE=yes ;;
	no-legend) LEGEND=no ;;
	skip-apt) SKIP="$SKIP apt" ;;
	skip-yum) SKIP="$SKIP yum" ;;
	skip-other) SKIP="$SKIP other" ;;
	product|product=*) optparse PRODUCT "$@" ;;
	prefix|prefix=*) optparse PREFIX "$@" ;;
	destination|destination=*) optparse DESTINATION "$@" ;;
	*) usage "illegal option -- ${OPTARG%=*}" ;;
      esac
      ;;
    :)  usage "option requires an argument -- $OPTARG" ;;
    \?) usage "illegal option -- $OPTARG" ;;
  esac
done

shift $((OPTIND - 1))

validate_product "$PRODUCT"

[[ -n $PREFIX      ]] || usage 'must specify a prefix'
[[ -d $PREFIX      ]] || usage 'directory not found:' "$PREFIX"
[[ -n $DESTINATION ]] || usage 'must specify a destination'

#
# Wraps rm but adds support for dry-run.
#
#   $1 - repository specification (e.g. production/debian)
#   $2 - source file
#
remove_file() {
  local NAME

  NAME=$(basename "$2")
  vprintf 'D [%s] %s\n' "$1" "$NAME"

  if [[ $DRYRUN = no ]]; then
    rm -f "$2"
    if [[ -z $REBUILD ]]; then
      REBUILD=$1
    elif [[ $REBUILD != *"$1"* ]]; then
      REBUILD=$REBUILD:$1
    fi
  fi
}

#
# Marks a file as skipped
#
#   $1 - repository specification (e.g. production/debian)
#   $2 - source file
#
skip_file() {
  local NAME

  NAME=$(basename "$2")
  vprintf 'S [%s] %s\n' "$1" "$NAME"
}

#
# Demote product files into the destination repository.
#
#   $* - package files
#
demote_file() {
  local SPEC

  SPEC="$DESTINATION/$PRODUCT"

  while [[ $# -gt 0 ]]; do
    if [[ $SKIP != *other* ]]; then
      remove_file "$SPEC" "$1"
    else
      skip_file "$SPEC" "$1"
    fi
    shift
  done
}

#
# Demote DEB packages into the destination repository.
#
#   $* - package files
#
demote_deb() {
  local SPEC PACKAGE

  SPEC="$DESTINATION/debian"

  while [[ $# -gt 0 ]]; do
    PACKAGE=$1; shift

    if [[ $SKIP != *apt* ]]; then
      case $PACKAGE in
	*.deb) remove_file "$SPEC" "$PACKAGE" ;;
	*) skip_file "$SPEC" "$PACKAGE" ;;
      esac
    else
      skip_file "$SPEC" "$PACKAGE"
    fi
  done
}

#
# Demote RPM packages into the destination repository.
#
#   $1 - package to demote
#
demote_rpm() {
  local SPEC PACKAGE

  SPEC="$DESTINATION/redhat"

  while [[ $# -gt 0 ]]; do
    PACKAGE=$1; shift

    if [[ $SKIP != *yum* ]]; then
      case $PACKAGE in
	*.rpm) remove_file "$SPEC" "$PACKAGE" ;;
	*) skip_file "$SPEC" "$PACKAGE" ;;
      esac
    else
      skip_file "$SPEC" "$PACKAGE"
    fi
  done
}

OUTFILE=$(mktemp -t demote.XXXXXX)
trap 'rm -f $OUTFILE' EXIT

while [[ $# -gt 0 ]]; do
  PKG=$1; shift

  case $PKG in
    *.deb) demote_deb "$PKG" ;;
    *.rpm) demote_rpm "$PKG" ;;
    *) demote_file "$PKG" ;;
  esac
done > "$OUTFILE"

COLORIZE_CMD=(sed)

# Prefer unbuffered output (-u) when available.
if [[ "$(uname -s)" != Darwin ]]; then
  COLORIZE_CMD+=(-u)
fi

COLORIZE_CMD+=(-f "$DEPLOY_HOME/libexec/colorize-changes.sed")

if [[ $COLORIZE = yes ]]; then
  sort "$OUTFILE" -k 2 | column -t | "${COLORIZE_CMD[@]}"
else
  sort "$OUTFILE" -k 2 | column -t
fi

if [[ $LEGEND = yes ]]; then
  vprintf \\n
  vprintf 'A = add, C = create, D = delete, M = modify, S = skip\n'
fi

if [[ $DRYRUN = no ]]; then
  IFS=: read -ra SPECS <<< "$REBUILD"
  "$DEPLOY_HOME/libexec/repoman-rebuild.bash" --prefix="$PREFIX" "${SPECS[@]}"
fi

exit
