#!/bin/bash

repo_url="$1"
agent_version="$2"

dos2unix /test/util.sh &>/dev/null && source /test/util.sh

print_header "Simple Package Install Test"

repo_name="newrelic"
# Special handling of the whole production/testing thing
if [[ "$repo_url" =~ 'testing' ]]; then
    repo_name="newrelic-testing"
fi

add_apt_repo "$repo_url" "$repo_name"

install_agent_from_repo "$agent_version"

verify_no_logs

dotnet run

verify_logs_exist