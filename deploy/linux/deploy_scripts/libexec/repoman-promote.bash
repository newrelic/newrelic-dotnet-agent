#!/bin/bash
# repoman-promote.bash - promote build artifacts from one deployment stage to another

### Usage: repoman-promote.bash --product=<product> --destination=<name>
###                             --prefix=<dir> [--skip-apt] [--skip-yum]
###                             [--skip-other] [--color] [--no-legend]
###                             [-n | --dry-run] [-v | --verbose] [--]
###                             <file>...
###        repoman-promote.bash [--help]
###
### Arguments:
###    <file>...              files to promote
###
### Options:
###    --product=<product>    product (php_agent or server_monitor)
###    --destination=<name>   where the files should be promoted
###    --prefix=<dir>         path to the directory containing the repository
###    --skip-apt             do not perform APT repository updates
###    --skip-yum             do not perform YUM repository updates
###    --skip-other           do not perform product repository updates
###    --color                colorize the output
###    --no-legend            suppress output of legend
###    -v, --verbose          cause promote to be verbose
###    -n, --dry-run          don't actually promote the build, just show the
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

#
# Print the path to a repository
#
repopath() {
  "$DEPLOY_HOME/libexec/repoman-path.bash" --prefix="$PREFIX" "$@"
}

declare DRYRUN=no VERBOSE=no COLORIZE=no LEGEND=yes
declare PRODUCT DESTINATION PREFIX SKIP REBUILD

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

[[ -n $PREFIX      ]] || usage 'must specify a prefix'
[[ -d $PREFIX      ]] || usage 'directory not found:' "$PREFIX"
[[ -n $DESTINATION ]] || usage 'must specify a destination'

validate_product "$PRODUCT"

#
# Wraps cp but adds support for dry-run.
#
#   $1 - repository specification (e.g. production/debian)
#   $2 - source file
#   $3 - destination
#
copy_file() {
  local STATUS NAME

  STATUS=A
  NAME=$(basename "$2")

  if [[ -d $3 ]]; then
    [[ -e "$3/$NAME" ]] && STATUS=M
  elif [[ -e $3 ]]; then
    STATUS=M
  fi

  vprintf '%s [%s] %s\n' $STATUS "$1" "$NAME"

  if [[ $DRYRUN = no ]]; then
    mkdir -p "$3"
    cp "$2" "$3"
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
skip_file() {
  local NAME

  NAME=$(basename "$2")
  vprintf 'S [%s] %s\n' "$1" "$NAME"
}

#
# Promote product files into the destination repository.
#
#   $* - package files
#
promote_file() {
  local SPEC

  SPEC="$DESTINATION/$PRODUCT"

  while [[ $# -gt 0 ]]; do
    if [[ $SKIP != *other* ]]; then
      copy_file "$SPEC" "$1" "$(repopath "$SPEC")"
    else
      skip_file "$SPEC" "$1"
    fi
    shift
  done
}

#
# Promote DEB packages into the destination repository.
#
#   $* - package files
#
promote_deb() {
  local SPEC ARCH_amd64 ARCH_i386 PACKAGE

  SPEC="$DESTINATION/debian"
  ARCH_amd64=$(repopath -d non-free -a amd64 "$SPEC")
  ARCH_i386=$(repopath -d non-free -a i386 "$SPEC")

  while [[ $# -gt 0 ]]; do
    PACKAGE=$1; shift

    if [[ $SKIP != *apt* ]]; then
      case $PACKAGE in
	*_amd64.deb)
          copy_file "$SPEC" "$PACKAGE" "$ARCH_amd64"
          ;;
	*_i386.deb)
          copy_file "$SPEC" "$PACKAGE" "$ARCH_i386"
          ;;
	*_all.deb)
          copy_file "$SPEC" "$PACKAGE" "$ARCH_amd64"
          copy_file "$SPEC" "$PACKAGE" "$ARCH_i386"
          ;;
	*)
	  skip_file "$SPEC" "$PACKAGE"
          ;;
      esac
    else
      skip_file "$SPEC" "$PACKAGE"
    fi
  done
}

#
# Promote RPM packages into the destination repository.
#
#   $1 - package to promote
#
promote_rpm() {
  local SPEC ARCH_amd64 ARCH_i386 PACKAGE

  SPEC="$DESTINATION/redhat"
  ARCH_amd64=$(repopath -d el7 -a x86_64 "$SPEC")
  ARCH_i386=$(repopath -d el7 -a i386 "$SPEC")

  while [[ $# -gt 0 ]]; do
    PACKAGE=$1; shift

    if [[ $SKIP != *yum* ]]; then
      case $PACKAGE in
	*.x86_64.rpm)
          copy_file "$SPEC" "$PACKAGE" "$ARCH_amd64"
          ;;
	*.i386.rpm)
          copy_file "$SPEC" "$PACKAGE" "$ARCH_i386"
          ;;
	*.noarch.rpm)
          copy_file "$SPEC" "$PACKAGE" "$ARCH_amd64"
          copy_file "$SPEC" "$PACKAGE" "$ARCH_i386"
          ;;
	*)
          skip_file "$SPEC" "$PACKAGE"
          ;;
      esac
    else
      skip_file "$SPEC" "$PACKAGE"
    fi
  done
}

OUTFILE=$(mktemp -t promote.XXXXXX)
trap 'rm -f $OUTFILE' EXIT

while [[ $# -gt 0 ]]; do
  PKG=$1; shift

  case $PKG in
    *.deb) promote_deb "$PKG" ;;
    *.rpm) promote_rpm "$PKG" ;;
    *) promote_file "$PKG" ;;
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
