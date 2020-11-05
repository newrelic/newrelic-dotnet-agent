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

set -e

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

  pushd "$REPO_DIR/../.." >/dev/null

  config_files_root='/data/deploy_scripts/conf'
  release_conf_file="${config_files_root}/release.conf"
  generate_conf_file="${config_files_root}/generate.conf"
  if [[ "$TARGET" == "testing" ]]
  then
    release_conf_file="${config_files_root}/release-testing.conf"
    generate_conf_file="${config_files_root}/generate-testing.conf"
  fi
  apt-ftparchive generate -c "$release_conf_file" "$generate_conf_file"
  apt-ftparchive release  -c "$release_conf_file" "dists/$REPO" > "dists/$REPO/Release"

  tar -xjvf "$GPG_KEYS"

  rm -f "dists/$REPO/Release.gpg"

  gpg -abs --digest-algo SHA256 --keyring gpg-conf/pubring.gpg --secret-keyring gpg-conf/secring.gpg -o "dists/$REPO/Release.gpg" "dists/$REPO/Release"
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
      createrepo --update --checksum sha "$REPO_DIR"
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
      rebuild_apt "$SPEC" 2>&1 | sed "${SED_ARGS[@]}" -e "/./ { s|^|[$SPEC] | }"
      ;;
    */redhat)
      rebuild_yum "$SPEC" 2>&1 | sed "${SED_ARGS[@]}" -e "/./ { s|^|[$SPEC] | }"
      ;;
    *)
      if [[ -n "$SPEC" ]]; then
        printf \\n
        printf '[%s]  nothing to do\n' "$SPEC"
      fi
      ;;
  esac
done
