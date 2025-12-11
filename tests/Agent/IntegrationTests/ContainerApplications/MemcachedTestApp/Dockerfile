ARG DOTNET_VERSION
ARG BUILD_ARCH
FROM --platform=amd64 mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS base
WORKDIR /app
EXPOSE 80

# build arch may be different from target arch (i.e., when running locally with QEMU)
ARG DOTNET_VERSION
ARG BUILD_ARCH
FROM --platform=${BUILD_ARCH} mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build

WORKDIR /src
COPY ["MemcachedTestApp/MemcachedTestApp.csproj", "MemcachedTestApp/"]
ARG APP_DOTNET_VERSION
RUN dotnet restore "MemcachedTestApp/MemcachedTestApp.csproj" -p:TargetFramework=net${APP_DOTNET_VERSION} -p:TargetFrameworks=net${APP_DOTNET_VERSION} --runtime linux-x64

COPY . .
WORKDIR "/src/MemcachedTestApp"
RUN dotnet build "MemcachedTestApp.csproj" -c Release -o /app/build --self-contained --framework net${APP_DOTNET_VERSION} -p:TargetFramework=net${APP_DOTNET_VERSION} -p:TargetFrameworks=net${APP_DOTNET_VERSION} --runtime linux-x64 --no-restore


FROM build AS publish
ARG APP_DOTNET_VERSION
RUN dotnet publish "MemcachedTestApp.csproj" -c Release -o /app/publish --self-contained --framework net${APP_DOTNET_VERSION} -p:TargetFramework=net${APP_DOTNET_VERSION} -p:TargetFrameworks=net${APP_DOTNET_VERSION} --runtime linux-x64 --no-restore


FROM base AS final

# Enable the agent
ARG NEW_RELIC_HOST
ARG NEW_RELIC_LICENSE_KEY
ARG NEW_RELIC_APP_NAME

ENV CORECLR_ENABLE_PROFILING=1 \
CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
CORECLR_NEW_RELIC_HOME=/usr/local/newrelic-dotnet-agent \
CORECLR_PROFILER_PATH=/usr/local/newrelic-dotnet-agent/libNewRelicProfiler.so \
NEW_RELIC_HOST=${NEW_RELIC_HOST} \
NEW_RELIC_LICENSE_KEY=${NEW_RELIC_LICENSE_KEY} \
NEW_RELIC_APP_NAME=${NEW_RELIC_APP_NAME} \
NEW_RELIC_LOG_DIRECTORY=/app/logs

WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "MemcachedTestApp.dll"]
