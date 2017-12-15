#!/bin/bash

repo_url="$1"
agent_version="$2"

dos2unix /test/util.sh &>/dev/null && source /test/util.sh

print_header "Simple Package Install Test"

add_yum_repo "$repo_url"

install_agent_from_repo "$agent_version"

verify_no_logs

dotnet run

verify_logs_exist