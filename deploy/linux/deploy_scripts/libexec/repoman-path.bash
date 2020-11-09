#!/bin/bash
# repoman-path.bash - print the path to a repository

### Usage: repoman-path.bash [--prefix=<dir>] [-d <name>] [-a <arch>] [--] <spec>
###        repoman-path.bash [--help]
###
### Arguments:
###   <spec>                     repository specification (e.g. production/debian)
###
### Options:
###   -d <name>, --dist=<name>   repository distribution (e.g. non-free, el5)
###   -a <arch>, --arch=<arch>   repository architecture (e.g. i386)
###   --prefix=<dir>             path to the directory containing the repository
###   --help                     print this message and exit
###

set -e

if [[ ! -d $DEPLOY_HOME ]]; then
  printf '%s: DEPLOY_HOME is not defined\n' "$(basename "$0")" >&2
  exit 1
fi

# shellcheck source=lib/common.bash
source "$DEPLOY_HOME/lib/common.bash"

declare SPEC DIST ARCH PREFIX REPOPATH

while getopts ':a:d:-:' OPTNAME; do
  case $OPTNAME in
    a) ARCH=$OPTARG ;;
    d) DIST=$OPTARG ;;
    -)
      case $OPTARG in
	'help') usage ;;
	arch|arch=*) optparse ARCH "$@" ;;
	dist|dist=*) optparse DIST "$@" ;;
	prefix|prefix=*) optparse PREFIX "$@" ;;
	*) usage "illegal option -- ${OPTARG%=*}" ;;
      esac
      ;;
    :)  usage "option requires an argument -- $OPTARG" ;;
    \?) usage "illegal option -- $OPTARG" ;;
  esac
done

shift $((OPTIND - 1))

SPEC=$1

[[ -n $SPEC ]] || usage 'must specify a repository'

#
# Construct the path to an APT repository.
#
apt_path() {
  local APTPATH

  case $SPEC in
    production/*) APTPATH="$SPEC/dists/newrelic" ;;
    */*) APTPATH="$SPEC/dists/newrelic-${SPEC%/*}" ;;
  esac

  if [[ -n $DIST ]]; then
    APTPATH="$APTPATH/$DIST"
  fi

  if [[ -n $ARCH ]]; then
    if [[ -z $DIST ]]; then
      die 'a distribution is required to specify an architecture'
    fi
    APTPATH="$APTPATH/binary-$ARCH"
  fi

  echo "$APTPATH"
}

#
# Construct the path to a YUM repository.
#
yum_path() {
  local YUMPATH

  case $SPEC in
    production/*) YUMPATH="${SPEC%/*}/pub/newrelic" ;;
    */*) YUMPATH="${SPEC%/*}/pub/newrelic-${SPEC%/*}" ;;
  esac

  if [[ -n $DIST ]]; then
    YUMPATH="$YUMPATH/$DIST"
  fi

  if [[ -n $ARCH ]]; then
    if [[ -z $DIST ]]; then
      die 'a distribution is required to specify an architecture'
    fi
    YUMPATH="$YUMPATH/$ARCH"
  fi

  echo "$YUMPATH"
}

#
# Construct the path to a product archive.
#
archive_path() {
  local ARPATH="production/${SPEC#*/}/archive"

  [[ -z $ARCH ]] || die 'architecture not supported for archives'

  if [[ -n $DIST ]]; then
    ARPATH="$ARPATH/$DIST"
  fi

  echo "$ARPATH"
}

#
# Construct the path to a product file repository.
#
product_path() {
  [[ -z $ARCH ]] || die 'architecture not supported for product repositories'
  [[ -z $DIST ]] || die 'distribution not supported for product repositories'

  case $SPEC in
    production/*)
      echo "$SPEC/release" ;;
    */*)
      echo "$SPEC/${SPEC%/*}" ;;
  esac
}

# SPEC should have the form repository/type_or_product
if [[ ${SPEC#*/} = */* ]]; then
  die 'invalid repository specification:' "$SPEC"
fi

case $SPEC in
  archive/*) REPOPATH="$(archive_path)" ;;
  */debian) REPOPATH="$(apt_path)" ;;
  */redhat) REPOPATH="$(yum_path)" ;;
  */*) REPOPATH="$(product_path)" ;;
  *) die 'invalid repository specification:' "$SPEC"
esac

if [[ -n $PREFIX ]]; then
  echo "${PREFIX%%/}/$REPOPATH"
else
  echo "$REPOPATH"
fi

exit
