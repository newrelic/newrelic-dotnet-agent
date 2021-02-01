#!/bin/bash
# archive.sh - promote build artifacts into an archive directory

### Usage: archive --product=<name> --version=<version> --prefix=<dir>
###                [--skip-apt] [--skip-yum] [--skip-other] [--color]
###                [--no-legend] [-n | --dry-run] [-v | --verbose] [--] <file>...
###        archive [--help]
###
### Arguments:
###   <files>...            files to archive
###
### Options:
###   --product=<name>      product (php_agent or server_monitor)
###   --version=<version>   version number
###   --prefix=<dir>        path to the directory containing the archive
###    --skip-apt           ignore DEB files
###    --skip-yum           ignore RPM files
###    --skip-other         ignore product files
###    --color              colorize the output
###    --no-legend          suppress output of legend
###    -v, --verbose        cause promote to be verbose
###    -n, --dry-run        don't actually archive the files, just show the
###                         steps that would be performed
###    --help               print this message and exit
###

set -e

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
declare PRODUCT VERSION PREFIX SKIP

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
        version|version=*) optparse VERSION "$@" ;;
	prefix|prefix=*) optparse PREFIX "$@" ;;
	*) usage "illegal option -- ${OPTARG%=*}" ;;
      esac
      ;;
    :)  usage "option requires an argument -- $OPTARG" ;;
    \?) usage "illegal option -- $OPTARG" ;;
  esac
done

shift $((OPTIND - 1))

validate_product "$PRODUCT"

[[ -n $VERSION ]] || usage 'must specify a version'
[[ -n $PREFIX  ]] || usage 'must specify a prefix'
[[ -d $PREFIX  ]] || usage 'directory not found:' "$PREFIX"

declare -r SPEC="archive/$PRODUCT"
declare -r DEST=$(repopath -d "$VERSION" "archive/$PRODUCT")

#
# Wraps cp but adds support for dry-run.
#
#   $1 - source file to copy
#
copy_file() {
  local STATUS NAME
  
  STATUS=A
  NAME=$(basename "$1")

  [[ -e "$DEST/$NAME" ]] && STATUS=M

  vprintf '%s [%s] %s\n' $STATUS "$SPEC" "$NAME"

  if [[ $DRYRUN = no ]]; then
    cp "$1" "$DEST"
  fi
}

#
# Marks a file as skipped
#
#   $1 - name of skipped file
#
skip_file() {
  local NAME

  NAME=$(basename "$1")
  vprintf 'S [%s] %s\n' "$SPEC" "$NAME"
}

process_files() {
  {
    if [[ ! -e "$DEST" ]]; then
      vprintf 'C [%s] %s\n' "$SPEC" "$DEST/"
      if [[ $DRYRUN = no ]]; then
	mkdir -p "$DEST"
      fi
    fi

    while [[ $# -gt 0 ]]; do
      PKG=$1; shift

      case $PKG in
	*.deb)
	  if [[ $SKIP != *apt* ]]; then
	    copy_file "$PKG" "$DEST"
	  else
	    skip_file "$PKG" "$DEST"
	  fi
	  ;;
	*.rpm)
	  if [[ $SKIP != *yum* ]]; then
	    copy_file "$PKG" "$DEST"
	  else
	    skip_file "$PKG" "$DEST"
	  fi
	  ;;
	*)
	  if [[ $SKIP != *other* ]]; then
	    copy_file "$PKG" "$DEST"
	  else
	    skip_file "$PKG" "$DEST"
	  fi
	  ;;
      esac
    done
  } | column -t
}

COLORIZE_CMD=(sed)

# Prefer unbuffered output (-u) when available.
if [[ "$(uname -s)" != Darwin ]]; then
  COLORIZE_CMD+=(-u)
fi

COLORIZE_CMD+=(-f "$DEPLOY_HOME/libexec/colorize-changes.sed")

if [[ $COLORIZE = yes ]]; then
  process_files "$@" | "${COLORIZE_CMD[@]}"
else
  process_files "$@"
fi

if [[ $LEGEND = yes ]]; then
  vprintf \\n
  vprintf 'A = add, C = create, D = delete, M = modify, S = skip\n'
fi

exit
