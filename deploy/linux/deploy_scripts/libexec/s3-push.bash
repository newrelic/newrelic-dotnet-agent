#!/bin/bash
# s3-push.bash - push contents of local working copy to S3

### Usage: s3-push.bash --prefix=<dir> [-n | --dry-run] [--] <repo> <bucket>
###        s3-push.bash [--help]
###
### Arguments:
###    <repo>        repo to deploy (e.g. production, testing)
###    <bucket>      name of s3 bucket to push to
###
### Options:
###    --product       product to publish (php_agent or server_monitor)
###    --prefix        directory prefix for the repository
###                    to publish
###    --help          show this message and exit
###    -n, --dry-run   don't actually publish the content, just show the
###                    steps that would be performed
###

set -e

if [[ ! -d $DEPLOY_HOME ]]; then
  printf '%s: DEPLOY_HOME is not defined\n' "$(basename "$0")" >&2
  exit 1
fi

# shellcheck source=lib/common.bash
source "$DEPLOY_HOME/lib/common.bash"

declare DRYRUN=no
declare PREFIX TARGET SOURCE_DIR DEST_BUCKET

[[ $# -eq 0 ]] && usage

while getopts ':n-:' OPTNAME; do
  case $OPTNAME in
    n) DRYRUN=yes ;;
    -)
      case $OPTARG in
	'help')	usage ;;
	dry-run) DRYRUN=yes ;;
	product|product=*) optparse PRODUCT "$@" ;;
	prefix|prefix=*) optparse PREFIX "$@" ;;
  *) usage "illegal option -- ${OPTARG%=*}" ;;
      esac
      ;;
    :)  usage "option requires an argument -- $OPTARG" ;;
    \?) usage "illegal option -- $OPTARG" ;;
  esac
done

shift $((OPTIND-1))

TARGET=$1
DEST_BUCKET=$2

[[ $# -gt 2 ]] && usage 'wrong number of parameters'

[[ -n $TARGET ]] || usage 'must specify a target'
[[ -n $PREFIX ]] || usage 'must specify a prefix'
[[ -n $DEST_BUCKET ]] || usage 'must specify a destination bucket'
[[ -d $PREFIX ]] || die 'directory not found:' "$PREFIX"

validate_product "$PRODUCT"

case $TARGET in
  production)
    SOURCE_DIR="$PREFIX/$TARGET"
    #DEST_BUCKET='PROD_TEST_S3'
    ;;
  testing)
    SOURCE_DIR="$PREFIX/$TARGET"
    #DEST_BUCKET='TEST_TEST_S3'
    ;;
  *)
    die 'target must be production or testing'
    ;;
esac

SYNC_EXTRA_ARGS=()

if [[ $DRYRUN = 'yes' ]]; then
  SYNC_EXTRA_ARGS+=(--dryrun)
fi

# Synchronizing local changes to S3 requires some subtlety. S3 does not
# support transactional updates nor does it support atomic writes. This means
# that clients can observe partially updated files! We attempt to workaround
# these limitations as best we can by observing that once published, package
# files should not be modified. Hence, the only modified files should be the
# metadata files that describe the contents of the repository to tools like
# APT and YUM. Files not described in those metadata files are invisible to
# those tools. Therefore, we perform the synchronization in three phases:
#
#   1. Upload new package files.
#   2. Upload modified package repository metadata files.
#   3. Remove deleted package files.
#
# Currently, we do not workaround the problem of observed partial writes.
# It is expected that the modified files are small enough and deploys rare
# enough to reduce the time window during which a partial update can be
# observed such that, in practice, no clients will be negatively affected.

# Be careful when making changes below. The aws tool is sensitive to the order
# of the --include and --exclude options. In short, the last match wins.
#
# See: http://docs.aws.amazon.com/cli/latest/reference/s3/index.html#use-of-exclude-and-include-filters

printf 'publishing package files\n'
printf '\n'

aws s3 sync "${SYNC_EXTRA_ARGS[@]}" \
    '--exclude=*' \
    '--include=debian/dists/*' \
    '--exclude=debian/dists/*/Contents*' \
    '--exclude=debian/dists/*/Packages*' \
    '--exclude=debian/dists/*/Release*' \
    '--include=pub/*' \
    '--exclude=pub/*/repodata/*' \
    "--include=$PRODUCT/*" \
    '--exclude=*.DS_Store' \
    "$SOURCE_DIR" \
    "$DEST_BUCKET"

printf '\n'
printf 'publishing package indexes\n'
printf '\n'

aws s3 sync "${SYNC_EXTRA_ARGS[@]}" \
    '--exclude=*' \
    '--include=debian/dists/*' \
    '--include=pub/*' \
    "--include=$PRODUCT/*" \
    '--exclude=*.DS_Store' \
    "$SOURCE_DIR" \
    "$DEST_BUCKET"

printf '\n'
printf 'unpublishing unreferenced package files\n'
printf '\n'

aws s3 sync "${SYNC_EXTRA_ARGS[@]}" --delete \
    '--exclude=*' \
    '--include=debian/dists/*' \
    '--include=pub/*' \
    "--include=$PRODUCT/*" \
    '--exclude=*.DS_Store' \
    "$SOURCE_DIR" \
    "$DEST_BUCKET"
