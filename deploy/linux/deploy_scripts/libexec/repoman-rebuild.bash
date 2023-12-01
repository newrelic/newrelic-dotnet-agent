#!/bin/bash
# repoman-rebuild.bash - scan a repository for changes

### Usage: repoman-rebuild.bash --prefix=<dir> [--] <spec>...
###        repoman-rebuild.bash [--help]
###
### Arguments:
###   <spec>                     repository specification (e.g. production/debian)
###
### Options:
###   -d <name>, --dist=<name>   repository distribution (e.g. non-free, el5)
###   --prefix=<dir>             path to the directory containing the repository
###   --help                     print this message and exit
###

# exit on any error
set -e
# exit a pipeline if any stage has an error
set -o pipefail

export TARGET
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

declare PREFIX SPEC

while getopts ':-:' OPTNAME; do
  case $OPTNAME in
    -)
      case $OPTARG in
	'help') usage ;;
	prefix|prefix=*) optparse PREFIX "$@" ;;
	*) usage "illegal option -- ${OPTARG%=*}" ;;
      esac
      ;;
    :)  usage "option requires an argument -- $OPTARG" ;;
    \?) usage "illegal option -- $OPTARG" ;;
  esac
done

shift $((OPTIND - 1))

[[ -n $PREFIX ]] || usage 'must specify a prefix'
[[ -d $PREFIX ]] || usage 'directory not found:' "$PREFIX"

#
# Rescan an APT repository to pick-up any changes.
#
#   $1 - repository specification
#
rebuild_apt() (
  printf \\n

  REPO_DIR=$(repopath "$1")
  REPO=$(basename "$REPO_DIR")

  echo "REPO_DIR=$REPO_DIR"
  pushd "$REPO_DIR/../.." >/dev/null
  
  config_files_root='/data/deploy_scripts/conf'
  release_conf_file="${config_files_root}/release.conf"
  generate_conf_file="${config_files_root}/generate.conf"
  if [[ "$TARGET" == "testing" ]]
  then
    release_conf_file="${config_files_root}/release-testing.conf"
    generate_conf_file="${config_files_root}/generate-testing.conf"
  fi
  echo "apt-ftparchive generate -c '$release_conf_file' '$generate_conf_file'"
  apt-ftparchive generate -c "$release_conf_file" "$generate_conf_file"
  echo "apt-ftparchive release -c '$release_conf_file' 'dists/$REPO' > 'dists/$REPO/Release'"
  apt-ftparchive release  -c "$release_conf_file" "dists/$REPO" > "dists/$REPO/Release"

  echo "untarring GPG_KEYS"
  tar -xjvf "$GPG_KEYS"

  echo "rm -f dists/$REPO/Release.gpg"
  rm -f "dists/$REPO/Release.gpg"

  echo "gpg signing"
  # We're using gpg1 (from the 'gnupg1' package) because newer versions of gpg (2.x+) do not support a separate secret key ring, and
  # the keys we use to sign the repo currently come as separate public and secret ring files
  gpg1 -abs --digest-algo SHA256 --keyring gpg-conf/pubring.gpg --secret-keyring gpg-conf/secring.gpg -o "dists/$REPO/Release.gpg" "dists/$REPO/Release"
  chmod 644 dists/"$REPO"/Contents-*.{gz,bz2}

  popd >/dev/null
)

#
# Rescan a YUM repository to pick-up any changes.
#
#   $1 - repository specification
#
rebuild_yum() {
   local ARCH REPO_DIR

  for ARCH in x86_64; do
    REPO_DIR=$(repopath -d el7 -a $ARCH "$1")

    printf \\n
    if [[ -d "$REPO_DIR" ]]; then
      createrepo_c --update --checksum sha "$REPO_DIR"
    fi
  done
}

SED_ARGS=()

# Prefer unbuffered output (-u) when available.
if [[ "$(uname -s)" != Darwin ]]; then
    SED_ARGS+=(-u)
fi

for SPEC; do
  case $SPEC in
    */debian)
      echo "rebuild_apt starting"
      rebuild_apt "$SPEC" 2>&1 | sed "${SED_ARGS[@]}" -e "/./ { s|^|[$SPEC] | }"
      echo "rebuild_apt done"
      ;;
    */redhat)
      echo "rebuild_yum starting"
      rebuild_yum "$SPEC" 2>&1 | sed "${SED_ARGS[@]}" -e "/./ { s|^|[$SPEC] | }"
      echo "rebuild_yum done"
      ;;
    *)
      if [[ -n "$SPEC" ]]; then
        printf \\n
        printf '[%s]  nothing to do\n' "$SPEC"
      fi
      ;;
  esac
done
