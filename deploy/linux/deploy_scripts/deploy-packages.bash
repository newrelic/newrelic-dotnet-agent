#!/bin/bash
# deploy-packages.bash - deploy or rollback .NET Core agent Linux packages from APT and YUM repos

### Usage: deploy-packages.bash --prefix=<dir> [--incoming-dir=<dir>]
###                        <action> <s3-bucket> <version>
###        deploy-packages.bash [--help]
###
### Arguments:
###   <action>              action to perform, must be 'release' or 'rollback'
###   <version>             agent version to relase or rollback
###
### Options:
###   --prefix=<dir>        directory containing the working copy of the
###                         download site
###   --incoming-dir        directory containing build artifacts to deploy
###   --help                print this message and exit
###
### Description:
###
###   Deploying or rolling back the .NET Core agent Linux packages proceeds
###   in three phases: pull, modify, push.
###   The three phases are designed to ensure the deploy process is safe,
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
###   deploying or rolling back the agent. During this phase, each of the
###   files comprising an agent release is copied to the appropriate location
###   within the download site. Additionally, the Debian and Redhat package
###   repositories are re-indexed and digitally signed. See libexec/repoman-*.bash for
###   details.
###
###   The third and final phase, the "push" phase, completes the deployment.
###   This phase uploads the changes made during the "modify" phase to the
###   download site. As in the "pull" phase, the AWS CLI[0] tools are used
###   perform the synchronization. See libexec/s3-push.bash for details.
###
###   [0] https://aws.amazon.com/cli/
###

# exit on any error
set -e
# exit a pipeline if any stage has an error
set -o pipefail

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
# Given a stream of file paths, prints the unique filenames.
#
unique_files_by_basename() {
  local REPLY

  # Split into (dirname basename) pairs, sort by basename,
  # uniqify by basename, then rejoin the pairs back into
  # complete paths.
  while read -r; do
    printf '%q:%q\n' "${REPLY%/*}" "${REPLY##*/}"
  done | sort -k 2 -t: -u | awk -F: '{print $1 "/" $2}'
}

PREFIX=
PRODUCT='dotnet_agent'
ACTION=
VERSION=

INCOMING_DIR=        # path of incoming package files (.deb and .rpm)
TARGET=              # name of the target repo

PRODUCT_REPO=        # path to the source product repo
APT_REPO=            # path to the source APT repo
YUM_REPO=            # path to the source YUM repo

# Note: having separate source and dest buckets is a holdover from the PHP agent's more complex process.  The .NET Agent team's process (as of 2018-04-26) only ever pulls and pushes from/to the same bucket for a given operation
SOURCE_BUCKET=       # s3:// URI for AWS S3 bucket to pull existing repo from
DEST_BUCKET=         # s3:// URI for AWS S3 bucket to push new repo to

while getopts ':i:p:a:s:v:' OPTNAME; do
  case $OPTNAME in
    i)
      INCOMING_DIR=$OPTARG
      ;;
    p)
      PREFIX=$OPTARG
      ;;
    a)
      ACTION=$OPTARG
      ;;
    s)
      S3_BUCKET=$OPTARG
      ;;
    v)
      VERSION=$OPTARG
      ;;
    :)  usage "option requires an argument -- $OPTARG" ;;
    \?) usage "illegal option -- $OPTARG" ;;
  esac
done

[[ -n $PREFIX && -d $PREFIX ]] || die 'must specify a working directory'
[[ -n $ACTION  ]] || die 'must specify an action'
[[ -n $S3_BUCKET ]] || die 'must specify an s3 bucket'
[[ -n $VERSION ]] || die 'must specify a version'

SOURCE_BUCKET="$S3_BUCKET"
DEST_BUCKET="$S3_BUCKET"

if [[ ! "$ACTION" =~ release|rollback ]]; then
  echo "ACTION must be 'release' or 'rollback'.  You said '$ACTION'.  Exiting."
  exit
fi

# All of the following commented out code comes from the PHP agent team's more complex process.  The TARGET variable doesn't actually control what S3
# bucket the packages are being deployed to or rolled back from, it's just used as a local path component by some sub-scripts (e.g. s3-pull/push)

# # Set the target (production vs. testing) based on the S3 bucket URI.  If the supplied bucket doesn't match one of the four we know about, bail out.
# if [ "$S3_BUCKET" == "$PROD_MAIN_S3" -o "$S3_BUCKET"  == "$PROD_TEST_S3" ]
# then
#   TARGET='production'
# elif [ "$S3_BUCKET" == "$TEST_PRIV_S3" -o "$S3_BUCKET"  == "$TEST_TEST_S3" ]
# then
#   TARGET='testing'
# else
#   echo "Specified S3 bucket uri '$S3_BUCKET' does not match any known buckets.  Exiting."
#   exit
# fi

# export TARGET

export TARGET='production' # this is just a string used in local paths for repository data pulled down from S3 and then pushed back up

# Make sure we have all the external tools we need
for CMD in apt-ftparchive gpg createrepo_c curl rsync; do
  if ! command -v $CMD > /dev/null; then
    die 'command not found:' $CMD
  fi
done

# Set a signal handler function to perform cleanup steps on exit
on_exit() {
  # This is leftover from the php agent team's version of this script; we don't need to do any cleanup at the moment
  echo "Exiting."
}
trap 'on_exit $?' EXIT


## BEGIN PULL PHASE ##
# Pull the specified S3 bucket down to the local filesystem
printf \\n
"$DEPLOY_HOME/libexec/s3-pull.bash" --prefix="$PREFIX" "$SOURCE_BUCKET" "$TARGET"
printf \\n
## END PULL PHASE ##

## BEGIN MODIFY PHASE ##

PRODUCT_NAME='newrelic-dotnet-agent'

if [[ "$ACTION" == 'release' ]]; then
  # Find the local package files to be deployed
  PRODUCT_REPO=$INCOMING_DIR
  APT_REPO=$INCOMING_DIR
  YUM_REPO=$INCOMING_DIR

  [[ -d $PRODUCT_REPO ]] || die 'product repository not found:' "$PRODUCT_REPO"
  [[ -d $APT_REPO     ]] || die 'APT repository not found:' "$APT_REPO"
  [[ -d $YUM_REPO     ]] || die 'YUM repository not found:' "$YUM_REPO"

  #
  # Find the .NET agent packages for the given version.
  #
  findpkgs() {
    {
      find "$APT_REPO" -name "${PRODUCT_NAME}_${VERSION}_amd64.deb"
      find "$APT_REPO" -name "${PRODUCT_NAME}_${VERSION}_arm64.deb"
      find "$YUM_REPO" -name "${PRODUCT_NAME}-${VERSION}-1.x86_64.rpm"
    } | unique_files_by_basename
  }

  FILES=( $(findpkgs) )

  if [[ ${#FILES[@]} -eq 0 ]]; then
    die "no files found for $VERSION"
  fi

  printf '\n'
  printf '>>> the following files will be added to %s\n' "$TARGET"
  printf '\n'
  printf '%s\n' "${FILES[@]}"

  printf '\n'
  printf '>>> promoting %s %s to %s...\n' "$PRODUCT" "$VERSION" "$TARGET"
  printf '\n'

  # The following commented-out call to repoman-demote is a carryover from the php agent team's version of this script.  In our usage since .NET Core was released, it never does anything useful.  Commenting out for now.
  # "$DEPLOY_HOME/libexec/repoman-demote.bash" \
  #     --prefix="$PREFIX" \
  #     --product="$PRODUCT" \
  #     --destination="$TARGET" \
  #     --verbose \
  #     --no-legend \
  #     "$(repopath "$TARGET/$PRODUCT")"/*
  # printf '\n'

  "$DEPLOY_HOME/libexec/repoman-promote.bash" \
      --prefix="$PREFIX" \
      --product="$PRODUCT" \
      --destination="$TARGET" \
      --verbose \
      "${FILES[@]}"

elif [[ "$ACTION" == 'rollback' ]]; then

  findpkgs() {
    {
      find "$PREFIX/$TARGET" -name "${PRODUCT_NAME}_${VERSION}_amd64.deb"
      find "$PREFIX/$TARGET" -name "${PRODUCT_NAME}_${VERSION}_arm64.deb"
      find "$PREFIX/$TARGET" -name "${PRODUCT_NAME}-${VERSION}-1.x86_64.rpm"
    } | unique_files_by_basename
  }

  FILES=( $(findpkgs) )

  if [[ ${#FILES[@]} -eq 0 ]]; then
    die "no files found for $VERSION"
  fi

  printf '\n'
  printf '>>> the following files will be removed from %s\n' "$TARGET"
  printf '\n'
  printf '%s\n' "${FILES[@]}"

  printf '\n'
  printf '>>> rolling back %s %s from %s...\n' "$PRODUCT" "$VERSION" "$TARGET"
  printf '\n'

  "$DEPLOY_HOME/libexec/repoman-demote.bash" \
    --prefix="$PREFIX" \
    --product="$PRODUCT" \
    --destination="$TARGET" \
    --verbose \
    "${FILES[@]}"
fi

## END MODIFY PHASE ##

## BEGIN PUSH PHASE ##
printf '\n'
printf '>>> pushing changes to S3...\n'
printf '\n'

"$DEPLOY_HOME/libexec/s3-push.bash" \
    --prefix="$PREFIX" \
    --product="$PRODUCT" \
    "$TARGET" "$DEST_BUCKET"

printf '\n'
printf '>>> %s %s %s to/from %s completed successfully.\n' \
       "$ACTION" "$PRODUCT" "$VERSION" "$TARGET"
### END PUSH PHASE ###

exit
