#!/bin/bash
# s3-pull.bash - update local working copy by syncronizing with S3

### Usage: s3-pull.bash [-n | --dry-run] --prefix=<dir> <bucket> <repo>
###        s3-pull.bash --help
###

set -e

if [[ ! -d $DEPLOY_HOME ]]; then
  printf '%s: DEPLOY_HOME is not defined\n' "$(basename "$0")" >&2
  exit 1
fi

# shellcheck source=lib/common.bash
source "$DEPLOY_HOME/lib/common.bash"

declare DRYRUN=no
declare PREFIX
declare SOURCE_BUCKET
declare TARGET

while getopts ':n-:' OPTNAME; do
  case $OPTNAME in
    n) DRYRUN=yes ;;
    -)
      case $OPTARG in
	'help') usage ;;
	dry-run) DRYRUN=yes ;;
	prefix|prefix=*) optparse PREFIX "$@" ;;
	*) usage "illegal option -- ${OPTARG%=*}" ;;
      esac
      ;;
    :)  usage "option requires an argument -- $OPTARG" ;;
    \?) usage "illegal option -- $OPTARG" ;;
  esac
done

shift $((OPTIND - 1))

SOURCE_BUCKET=$1
TARGET=$2

[[ $# -gt 2 ]] && usage 'wrong number of parameters'

[[ -n $PREFIX ]] || usage 'must specify a prefix'
[[ -n $SOURCE_BUCKET ]] || usage 'must specify a source bucket'
[[ -n $TARGET ]] || usage 'must specify a repo'
[[ -d $PREFIX ]] || usage 'directory not found:' "$PREFIX"

SYNC_EXTRA_ARGS=()

if [[ $DRYRUN = 'yes' ]]; then
  SYNC_EXTRA_ARGS+=(--dryrun)
fi

# Be careful when making changes below. The aws tool is sensitive to the order
# of the --include and --exclude options. In short, the last match wins.
#
# See: http://docs.aws.amazon.com/cli/latest/reference/s3/index.html#use-of-exclude-and-include-filters

printf '>>> synchronizing local copy %s with latest changes from %s\n' "$PREFIX/$TARGET" "$SOURCE_BUCKET"
printf '\n'

  aws s3 sync \
    "${SYNC_EXTRA_ARGS[@]}" \
    --delete \
    '--exclude=*' \
    '--include=debian/dists/*' \
    '--include=pub/*' \
    '--exclude=*.DS_Store' \
    "$SOURCE_BUCKET" \
    "$PREFIX/$TARGET" 


