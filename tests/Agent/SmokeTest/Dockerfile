FROM mcr.microsoft.com/dotnet/sdk:6.0

RUN apt-get update
RUN apt-get install -y less vim dos2unix

ENV CORECLR_ENABLE_PROFILING="1" \
CORECLR_PROFILER="{36032161-FFC0-4B61-B559-F6C5D41BAE5A}" \
CORECLR_NEWRELIC_HOME="/usr/local/newrelic-netcore20-agent" \
CORECLR_PROFILER_PATH="/usr/local/newrelic-netcore20-agent/libNewRelicProfiler.so"

COPY ./app /app
COPY ./run.sh /root
RUN dos2unix /root/run.sh
RUN chmod a+x /root/run.sh

WORKDIR /app

RUN dotnet build -c Release -r debian-x64

