#!/bin/bash
# jenkins-download.bash -- download build artifacts from Jenkins

### Usage: jenkins-download.bash --job=<name> [--build=<name-or-number>]
###                              [--label=<label-or-node>]
###                              [--directory-prefix=<prefix>]
###                              [-n | --dry-run] [-v | --verbose]
###        jenkins-download.bash [--help]
###
###    --job=<name>                  job name
###    --build=<name-or-number>      build number or build name
###                                  defaults to lastStableBuild
###    --label=<label-or-node>       label/node containing the build artifacts
###    --directory-prefix=<prefix>   directory where files will be saved
###                                  defaults to the current directory
###    --insecure                    disable SSL certificate verification
###    -n, --dry-run                 don't actually execute the command, just
###                                  show the steps that would be performed
###    -v, --verbose                 cause jenkins-download to be verbose
###    --help                        print this message and exit
###

set -e

if [[ ! -d $DEPLOY_HOME ]]; then
  printf '%s: DEPLOY_HOME is not defined\n' "$(basename "$0")" >&2
  exit 1
fi

# shellcheck source=lib/common.bash
source "$DEPLOY_HOME/lib/common.bash"

declare DRYRUN=no VERBOSE=no VERIFYCERTS=yes
declare JOB BUILD=lastSuccessfulBuild LABEL PREFIX=.

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
	job|job=*) optparse JOB "$@" ;;
	build|build=*) optparse BUILD "$@" ;;
	label|label=*) optparse LABEL "$@" ;;
	directory-prefix|directory-prefix=*) optparse PREFIX "$@" ;;
	*) usage "illegal option -- ${OPTARG%=*}" ;;
      esac
      ;;
    :) usage "option requires an argument -- $OPTARG" ;;
    \?) usage "illegal option -- $OPTARG" ;;
  esac
done

shift $((OPTIND - 1))

[[ -n $JOB ]] || usage 'must specify a job'

if [[ -n $LABEL ]]; then
  [[ -n $BUILD ]] || usage 'must specify a build to use a label'
elif [[ -z $BUILD ]]; then
  usage 'must specify a build'
fi

: "${JENKINS_SCHEME:=https}"
if [[ $JENKINS_SCHEME != http && $JENKINS_SCHEME != https ]]; then
  usage 'scheme must be http or https'
fi

[[ -n $JENKINS_HOST ]] || usage 'must specify a host'

#
# Prints a list of artifact urls for a job, one per line.
#
#   $1 - base url of the build
#
artifacts() {
  local SCRIPT BASEURL=$1

  SCRIPT=$(cat <<EOF
doc = REXML::Document.new \$stdin

doc.elements.each("//artifact/relativePath") do |elt|
  puts "${BASEURL}/artifact/#{elt.text}"
end
EOF
)

  curl --silent --fail "$BASEURL/api/xml?tree=artifacts\[relativePath\]" \
    | ruby -r 'rexml/document' -e "$SCRIPT"

  if [[ ${PIPESTATUS[0]} -ne 0 || ${PIPESTATUS[1]} -ne 0 ]]; then
    printf >&2 \\n
    printf >&2 "failed to retrieve the list of build artifacts\n"
    printf >&2 "please verify the job, build and label are correct\n"
    printf >&2 "url=%s\\n" "$BASEURL"
    return 1
  fi
}

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
    curl --fail $EXTRA "$@"
  else
    echo curl --fail $EXTRA "$@"
  fi
}

vprintf 'fetching artifact list...'
if [[ -z $LABEL ]]; then
  ARTIFACTS=$(artifacts "$JENKINS_SCHEME://$JENKINS_HOST/job/$JOB/$BUILD")
else
  ARTIFACTS=$(artifacts "$JENKINS_SCHEME://$JENKINS_HOST/job/$JOB/$BUILD/label=$LABEL")
fi
vprintf 'done\n'

for URL in $ARTIFACTS; do
  ARTIFACT=$(basename "$URL")
  vprintf 'downloading %s\n' "$ARTIFACT"
  perform_curl --silent -o "$PREFIX/$ARTIFACT" "$URL"
done
