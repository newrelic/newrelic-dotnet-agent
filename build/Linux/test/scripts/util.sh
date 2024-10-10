#!/bin/bash
source /test/osht.sh

PACKAGE_NAME='newrelic-dotnet-agent'

function print_header {
    printf "\n\n\e[34m### $1\033[0m\n"
}

function check_deb_install_status {
    agent_version="$1"
    install_status=$(dpkg -s "${PACKAGE_NAME}" |grep Status)
    install_version=$(dpkg-query --showformat='${Version}' --show "${PACKAGE_NAME}")
    IS "$install_status" == "Status: install ok installed"
    if [ ! -z "$agent_version" ]; then
        IS "$install_version" == "$agent_version"
    fi
}

function check_rpm_install_status {
    agent_version="$1"
    install_status=$(rpm -q --qf "%{VERSION}" "${PACKAGE_NAME}")
    ISNT "$install_status" =~ "is not installed"
    if [ ! -z "$agent_version" ]; then
        IS "$agent_version" == "$install_status"
    fi
}

# install the .deb package
function install_debian_no_env {
    latest_deb=$(ls -1 /release/${PACKAGE_NAME}*_amd64.deb |tail -n 1)
    OK -n "$latest_deb"
    IS "$latest_deb" =~ deb
    dpkg -i "$latest_deb"
    check_deb_install_status
}

# install the .rpm package
function install_rpm_no_env {
    latest_rpm=$(ls -1 /release/${PACKAGE_NAME}*.x86_64.rpm |tail -n 1)
    OK -n "$latest_rpm"
    IS "$latest_rpm" =~ rpm
    rpm -ivh "$latest_rpm"
    check_rpm_install_status
}

function install_agent_no_env {
    if [[ -e /etc/redhat-release ]]; then
        install_rpm_no_env
    else
        install_debian_no_env
    fi
}

# install the platform-appropriate agent package and set the environment
function install_agent {
    install_agent_no_env
    source /etc/profile.d/${PACKAGE_NAME}-path.sh
    source /usr/local/${PACKAGE_NAME}/setenv.sh
}

function install_tarball {
    install_path="${1:-/usr/local}"
    if [[ ! -d "$install_path" ]]; then
        mkdir -p "$install_path"
    fi
    pushd "$install_path"
    latest_tarball=$(ls -1 /release/${PACKAGE_NAME}*.tar.gz |tail -n 1)
    echo "latest_tarball=$latest_tarball"
    OK -n "$latest_tarball"
    IS "$latest_tarball" =~ tar
    tar xvfz "$latest_tarball"
    popd
    export CORECLR_NEW_RELIC_HOME="${install_path}/${PACKAGE_NAME}"
    echo "install_tarball CORECLR_NEW_RELIC_HOME=${CORECLR_NEW_RELIC_HOME}"
    source "${install_path}/${PACKAGE_NAME}/setenv.sh"
}

function add_apt_repo {
    repo_url="$1"
    repo_name="$2"
    echo "deb ${repo_url} ${repo_name} non-free" | tee /etc/apt/sources.list.d/newrelic.list
    wget -O- https://download.newrelic.com/548C16BF.gpg | apt-key add -
    apt-get update
    cache_search=$(apt-cache search "${PACKAGE_NAME}")
    IS "$cache_search" =~ "${PACKAGE_NAME}"
}

function add_yum_repo {
    repo_url="$1"
    cat << HERE | tee "/etc/yum.repos.d/${PACKAGE_NAME}.repo"
[newrelic-test-repo-testing]
name=New Relic .NET Agent package(s) for Enterprise Linux
baseurl=${repo_url}/\$basearch/
enabled=1
gpgcheck=1
gpgkey=file:///etc/pki/rpm-gpg/RPM-GPG-KEY-NewRelic
HERE
    wget -O- https://download.newrelic.com/548C16BF.gpg | tee /etc/pki/rpm-gpg/RPM-GPG-KEY-NewRelic
    cache_search=$(yum search "${PACKAGE_NAME}")
    ISNT "$cache_search" =~ "Warning"
}

function install_agent_from_repo_no_env {
    agent_version="$1"
    if [[ -e /etc/redhat-release ]]; then
        yum install -y "${PACKAGE_NAME}"
        check_rpm_install_status "$agent_version"
    else
        apt-get install -y "${PACKAGE_NAME}"
        check_deb_install_status "$agent_version"
    fi
}

function install_agent_from_repo {
    agent_version="$1"
    install_agent_from_repo_no_env "$agent_version"
    source /etc/profile.d/${PACKAGE_NAME}-path.sh
    source /usr/local/${PACKAGE_NAME}/setenv.sh   
}

function good {
    echo -e "\e[32m$1\033[0m"
}

function bad {
    echo -e "\e[31m$1\033[0m"
    exit 1
}

function verify_no_logs {
    log_dir="${CORECLR_NEW_RELIC_HOME}/logs"
    log_file_count=$(ls -A1 "$log_dir" |wc -l)
    IS "$log_file_count" == 0
    if [[ "$log_file_count" -gt 0 ]]; then
        bad "$log_dir is not empty"
    else
        good "$log_dir is empty"
    fi
}

function verify_logs_exist {
    log_dir="${CORECLR_NEW_RELIC_HOME}/logs"
    log_file_count=$(ls -A1 "$log_dir" |wc -l)
    IS "$log_file_count" != 0
    if [[ "$log_file_count" -gt 0 ]]; then
        good "Verified log files were created"
    else
        bad "$log_dir is empty!"
    fi
}

function verify_agent_log_exists {
    app_name="$1"
    logfile_name="$CORECLR_NEW_RELIC_HOME/logs/newrelic_agent_${app_name}.log"
    OK -e "$logfile_name"
    if [[ -e "$logfile_name" ]]; then
        good "Verified agent log file $logfile_name was created"
    else
        bad "No agent log file!"
    fi
}

function verify_agent_log_grep {
    count=$(grep "$1" ${CORECLR_NEW_RELIC_HOME}/logs/* |wc -l)
    IS "$count" != 0
    if [[ "$count" -gt 0 ]]; then
        good "$1 was in the log files"
    else
        bad "$1 was not in the log files!"
    fi
}
