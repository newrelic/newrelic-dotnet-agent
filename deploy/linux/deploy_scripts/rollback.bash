#!/bin/bash
# rollback.bash - rollback a product release

### Usage: rollback.bash --prefix=<dir> --product=<name> <target> <version>
###        rollback.bash [--help]
###
### Arguments:
###   <target>              target repository (production or testing)
###   <version>             agent version to rollback
###
### Options:
###   --prefix=<dir>        path to the directory containing the archive
###   --product=<name>      product (php_agent or server_monitor)
###   --help                print this message and exit
###
### Description:
###
###   Rolling back a release proceeds in three phases: pull, modify, push.
###   The three phases are designed to ensure the rollback process is safe,
###   reliable, and atomic. i.e. Changes should not be visible to customers
###   unless they are both complete and correct. To achieve these goals, this
###   script never makes direct changes to the public download website.
###
###   The first phase, the "pull" phase, ensures that a local copy of the
###   download site is available and up-to-date. The download site is
###   backed by Amazon S3, so this phase uses the AWS CLI[0] tools to
###   synchronize the local copy with the current contents of the public
###   download site. Warning: Synchronizing will remove any files present
###   in the local copy, but not present on the download site.
###   See libexec/s3-pull.bash for details.
###
###   The second phase, the "modify" phase, performs the real work of
###   rolling back a release. During this phase, each of the files comprising
###   an agent release is removed from the local copy. Additionally, the Debian
###   and Redhat package repositories are re-indexed and digitally signed.
###   See libexec/repoman-*.bash for details.
###
###   The third and final phase, the "push" phase, completes the rollback.
###   This phase uploads the changes made during the "modify" phase to the
###   download site. As in the "pull" phase, the AWS CLI[0] tools are used
###   perform the synchronization. See libexec/s3-push.bash for details.
###
###   [0] https://aws.amazon.com/cli/
###

set -e

# Determine our location in the filesystem.
# See: http://stackoverflow.com/questions/59895/can-a-bash-script-tell-what-directory-its-stored-in
pushd . > /dev/null
  DEPLOY_HOME=${BASH_SOURCE[0]}

  if [[ -h $DEPLOY_HOME ]]; then
    while [[ -h $DEPLOY_HOME ]]; do
      cd "$(dirname "$DEPLOY_HOME")"
      DEPLOY_HOME=$(readlink "$DEPLOY_HOME")
    done
  fi

  cd "$(dirname "$DEPLOY_HOME")" > /dev/null
  DEPLOY_HOME=$(pwd)
popd > /dev/null

export DEPLOY_HOME

# shellcheck source=lib/common.bash
source "$DEPLOY_HOME/lib/common.bash"

#
# Print the path to a repository
#
repopath() {
  "$DEPLOY_HOME/libexec/repoman-path.bash" --prefix="$PREFIX" "$@"
}

#
# Print the files associated with the current product and version.
#
findpkgs() {
  local PRODUCT_REPO APT_REPO YUM_REPO

  PRODUCT_REPO=$(repopath "$TARGET/$PRODUCT")
  APT_REPO=$(repopath "$TARGET/debian")
  YUM_REPO=$(repopath --dist=el7 "$TARGET/redhat")

  case $PRODUCT in
    php_agent)
      find "$PRODUCT_REPO" -name "newrelic-php5-${VERSION}-*.tar.gz"
      find "$APT_REPO" -name "newrelic-daemon_${VERSION}_*.deb" \
	   -or -name "newrelic-php5_${VERSION}_*.deb" \
	   -or -name "newrelic-php5-common_${VERSION}_*.deb"
      find "$YUM_REPO" -name "newrelic-daemon-${VERSION}-*.rpm" \
	   -or -name "newrelic-php5-${VERSION}-*.rpm" \
	   -or -name "newrelic-php5-common-${VERSION}-*.rpm"
      ;;

    server_monitor)
      find "$PRODUCT_REPO" -name "newrelic-sysmond-${VERSION}-*.tar.gz"
      find "$APT_REPO" -name "newrelic-sysmond_${VERSION}_*.deb"
      find "$YUM_REPO" -name "newrelic-sysmond-${VERSION}-*.rpm"
      ;;
  esac
}

PREFIX=
PRODUCT=
TARGET=
VERSION=

while getopts ':nv-:' OPTNAME; do
  case $OPTNAME in
    -)
      case $OPTARG in
	'help') usage ;;
	prefix|prefix=*) optparse PREFIX "$@" ;;
	product|product=*) optparse PRODUCT "$@" ;;
	*) usage "illegal option -- ${OPTARG%=*}" ;;
      esac
      ;;
    :)  usage "option requires an argument -- $OPTARG" ;;
    \?) usage "illegal option -- $OPTARG" ;;
  esac
done

shift $((OPTIND - 1))

[[ $# -eq 2 ]] || usage

TARGET=$1
VERSION=$2

[[ -n $PREFIX && -d $PREFIX ]] || die 'must specify a working directory'
[[ -n $PRODUCT ]] || die 'must specify the product'
[[ -n $VERSION ]] || die 'must specify the version'

case $TARGET in
  production|testing) ;;
  *) die 'invalid target' "$TARGET" ;;
esac

printf '\n'

"$DEPLOY_HOME/libexec/s3-pull.bash" --prefix="$PREFIX"

printf '\n'
printf '>>> rolling back %s %s from %s...\n' "$PRODUCT" "$VERSION" "$TARGET"

FILES=( $(findpkgs) )

if [[ ${#FILES[@]} -eq 0 ]]; then
  die "no files found for $PRODUCT $VERSION"
fi

printf '\n'
printf 'the following files will be removed\n'
printf '%s\n' "${FILES[@]}"

printf '\n'

"$DEPLOY_HOME/libexec/repoman-demote.bash" \
    --prefix="$PREFIX" \
    --product="$PRODUCT" \
    --destination="$TARGET" \
    --verbose \
    "${FILES[@]}"

printf '\n'
printf '>>> pushing changes to S3...\n'
printf '\n'

"$DEPLOY_HOME/libexec/s3-push.bash" \
    --prefix="$PREFIX" \
    --product="$PRODUCT" \
    "$TARGET"

printf '\n'
printf '>>> Rollback of %s %s from %s completed successfully.\n' \
       "$PRODUCT" "$VERSION" "$TARGET"

exit
