ARG DISTRO_TAG
ARG BUILD_ARCH

# TODO Use stable .NET 10.0 image when available
FROM --platform=amd64 mcr.microsoft.com/dotnet/aspnet:10.0-preview-trixie-slim AS base
WORKDIR /app
EXPOSE 80
# TODO: use packages.microsoft.com/config/debian/13 when available
RUN apt-get update \
    && apt-get install -y wget \
    && wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y aspnetcore-runtime-8.0

# build arch may be different from target arch (i.e., when running locally with QEMU)
# TODO Use stable .NET 10.0 image when available
FROM --platform=${BUILD_ARCH} mcr.microsoft.com/dotnet/sdk:10.0-preview-trixie-slim AS build
# TODO: use packages.microsoft.com/config/debian/13 when available
RUN apt-get update \
    && apt-get install -y wget \
    && wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y dotnet-sdk-8.0
WORKDIR /src
COPY ["MemcachedTestApp/MemcachedTestApp.csproj", "MemcachedTestApp/"]
ARG APP_DOTNET_VERSION
RUN dotnet restore "MemcachedTestApp/MemcachedTestApp.csproj" -p:TargetFramework=net${APP_DOTNET_VERSION}

COPY . .
WORKDIR "/src/MemcachedTestApp"
RUN dotnet build "MemcachedTestApp.csproj" -c Release -o /app/build --self-contained --os linux --framework net${APP_DOTNET_VERSION}


FROM build AS publish
ARG APP_DOTNET_VERSION
RUN dotnet publish "MemcachedTestApp.csproj" -c Release -o /app/publish --self-contained --os linux --framework net${APP_DOTNET_VERSION}


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
