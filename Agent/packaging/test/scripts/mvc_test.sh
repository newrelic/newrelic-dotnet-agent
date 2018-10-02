#!/bin/bash

dos2unix /test/util.sh &>/dev/null && source /test/util.sh

print_header "Kestrel Test"

install_agent

verify_no_logs

CORECLR_ENABLE_PROFILING=0 dotnet build
dotnet bin/Debug/netcoreapp2.1/mvc.dll &

until $(curl --output /dev/null --silent --head --fail http://localhost:5000); do
    printf '.'
    sleep 5
done

verify_logs_exist

verify_agent_log_exists "mvc"

