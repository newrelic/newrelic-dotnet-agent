ARG DOTNET_VERSION=9.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
COPY ["PerformanceTestApp.csproj", "./"]
RUN dotnet restore "PerformanceTestApp.csproj"
COPY . .
RUN dotnet publish "PerformanceTestApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app
EXPOSE 8080

# Install curl for the Docker health check
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Agent environment variables - only take effect when the agent is volume-mounted at
# /usr/local/newrelic-dotnet-agent. When running without the agent, set
# CORECLR_ENABLE_PROFILING=0 (the default) or simply omit the volume mount.
ENV CORECLR_ENABLE_PROFILING=0 \
    CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
    CORECLR_NEWRELIC_HOME=/usr/local/newrelic-dotnet-agent \
    CORECLR_PROFILER_PATH=/usr/local/newrelic-dotnet-agent/libNewRelicProfiler.so \
    NEW_RELIC_LOG_DIRECTORY=/app/logs \
    ASPNETCORE_ENVIRONMENT=Production

RUN mkdir -p /app/logs

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PerformanceTestApp.dll"]
