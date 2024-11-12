#!/bin/bash

dos2unix /test/util.sh &>/dev/null && source /test/util.sh

print_header "Custom XML Instrumentation Test"

install_agent

verify_no_logs

dos2unix Instrumentation.xml &>/dev/null
cp Instrumentation.xml $CORECLR_NEW_RELIC_HOME/extensions/
CORECLR_ENABLE_PROFILING=0 dotnet build
dotnet bin/Debug/net6.0/custom_xml.dll

verify_logs_exist

verify_agent_log_exists "custom_xml"

verify_agent_log_grep "Instrumenting.*Program.Transaction"