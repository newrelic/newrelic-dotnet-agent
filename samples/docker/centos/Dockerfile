FROM quay.io/centos/centos:stream9 AS base
WORKDIR /app
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80

# build using the Debian-based .NET SDK image
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build 

WORKDIR /src
COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish 

FROM base AS final

# install asp.netcore
RUN dnf install aspnetcore-runtime-7.0 -y

# install wget
RUN yum -y upgrade && yum -y install wget

# download and install the New Relic .NET Agent via the rpm package manager
# find the latest version at https://download.newrelic.com/dot_net_agent/latest_release and update the filename as needed
# the agent is installed at /usr/local/new-relic-dotnet-agent
RUN wget -L https://download.newrelic.com/dot_net_agent/latest_release/newrelic-dotnet-agent-10.15.0-1.x86_64.rpm
RUN rpm -i newrelic-dotnet-agent-10.15.0-1.x86_64.rpm

# Enable the agent
ENV CORECLR_ENABLE_PROFILING=1 \
CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
CORECLR_NEWRELIC_HOME=/usr/local/newrelic-dotnet-agent \
CORECLR_PROFILER_PATH=/usr/local/newrelic-dotnet-agent/libNewRelicProfiler.so \
NEW_RELIC_LICENSE_KEY=<your-new-relic-license-key> \
NEW_RELIC_APP_NAME=weather-forecast-centos \
NEWRELIC_LOG_DIRECTORY=/app/logs

WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "WeatherForecast.dll"]