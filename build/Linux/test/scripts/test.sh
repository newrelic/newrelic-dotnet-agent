#!/bin/bash

dos2unix /test/util.sh &>/dev/null && source /test/util.sh

print_header "Simple Package Install Test"

install_agent

echo "see env vars"
export

verify_no_logs

dotnet run

echo "see logs dir"
ls /usr/local/newrelic-netcore20-agent/logs

verify_logs_exist