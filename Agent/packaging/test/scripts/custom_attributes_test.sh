#!/bin/bash

dos2unix /test/util.sh &>/dev/null && source /test/util.sh

print_header "Custom Attribute Instrumentation Test"

install_agent

verify_no_logs

CORECLR_ENABLE_PROFILING=0 dotnet build
dotnet bin/Debug/netcoreapp2.1/custom_attributes.dll

verify_logs_exist

verify_agent_log_exists "custom_attributes"

verify_agent_log_grep "Instrumenting.*Program.Transaction"