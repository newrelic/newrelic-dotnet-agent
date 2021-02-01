#!/bin/bash
# jenkins-build.bash - trigger a jenkins job

### Usage: jenkins-build.bash [--host=<host>] [--user=<user>] [--token=<token>]
###                           [-n | --dry-run] [-v | --verbose] [--] <job>...
###        jenkins-build.bash [--help]
###
### Arguments:
###    <job>...            jobs to trigger
###
### Options:
###    --insecure          disable SSL certificate verification
###    --host=<host>       hostname of the jenkins server
###    --token=<token>     jenkins API token
###    --user=<user>       user for the jenkins API token
###    -n, --dry-run       don't actually execute the command, just show the
###                        steps that would be performed
###    -v, --verbose       cause jenkins-build.bash to be verbose
###    --help              print this message and exit
###

set -e

if [[ ! -d $DEPLOY_HOME ]]; then
  printf '%s: DEPLOY_HOME is not defined\n' "$(basename "$0")" >&2
  exit 1
fi

# shellcheck source=lib/common.bash
source "$DEPLOY_HOME/lib/common.bash"

declare DRYRUN=no VERBOSE=no VERIFYCERTS=yes

while getopts ':nv-:' OPTNAME; do
  case $OPTNAME in
    n) DRYRUN=yes ;;
    v) VERBOSE=yes ;;
    -)
      case $OPTARG in
	'help') usage ;;
	dry-run) DRYRUN=yes ;;
	verbose) VERBOSE=yes ;;
	insecure) VERIFYCERTS=no ;;
	host|host=*) optparse JENKINS_HOST "$@" ;;
	user|user=*) optparse JENKINS_USER "$@" ;;
	token|token=*) optparse JENKINS_TOKEN "$@" ;;
	*) usage "illegal option -- ${OPTARG%=*}" ;;
      esac
      ;;
    :)  usage "option requires an argument -- $OPTARG" ;;
    \?) usage "illegal option -- $OPTARG" ;;
  esac
done

shift $((OPTIND - 1))

[[ -n $JENKINS_HOST   ]] || usage 'must specify a host'
[[ -n $JENKINS_USER   ]] || usage 'must specify an auth user'
[[ -n $JENKINS_TOKEN  ]] || usage 'must specify an auth token'

#
# Executes a curl command unless we're performing a dry-run, in which
# case just print the command that would be executed.
#
perform_curl() {
  local EXTRA

  if [[ $VERIFYCERTS = no ]]; then
    EXTRA='--insecure'
  fi

  if [[ $DRYRUN = no ]]; then
    curl --silent --fail $EXTRA "$@"
  else
    echo curl --silent --fail $EXTRA "$@"
  fi
}

while [[ $# -gt 0 ]]; do
  if [[ -n $1 ]]; then
    vprintf "triggering build for '%s'...\n" "$1"
    perform_curl -X POST --basic --user "$JENKINS_USER:$JENKINS_TOKEN" "https://$JENKINS_HOST/job/$1/build"
  fi
  shift
done

exit
