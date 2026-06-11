ARG DOTNET_VERSION
ARG DISTRO_TAG
ARG TARGET_ARCH
ARG BUILD_ARCH
FROM --platform=${TARGET_ARCH} mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-${DISTRO_TAG} AS base
WORKDIR /app
EXPOSE 80

# build arch may be different from target arch (i.e., when running locally with QEMU)
FROM --platform=${BUILD_ARCH} mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-${DISTRO_TAG} AS build
ARG TARGET_ARCH
WORKDIR /src
COPY ["W3CTestApp/W3CTestApp.csproj", "W3CTestApp/"]
RUN dotnet restore "W3CTestApp/W3CTestApp.csproj"  -a ${TARGET_ARCH}

COPY . .
WORKDIR "/src/W3CTestApp"
RUN dotnet build "W3CTestApp.csproj" -c Release -o /app/build --os linux -a ${TARGET_ARCH}

FROM build AS publish
RUN dotnet publish "W3CTestApp.csproj" -c Release -o /app/publish /p:UseAppHost=false --os linux -a ${TARGET_ARCH}

FROM base AS final

# Install Python, aiohttp, and the pinned W3C trace-context test suite. These run inside the
# container so the test host no longer needs Python installed. The repo is pinned to a known-good
# commit to keep the validation suite stable.
RUN apt-get update \
 && apt-get install -y --no-install-recommends python3 python3-aiohttp git ca-certificates \
 && rm -rf /var/lib/apt/lists/*
RUN git clone https://github.com/w3c/trace-context.git /trace-context \
 && git -C /trace-context checkout 98f210efd89c63593dce90e2bae0a1bdcb986f51

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

ENTRYPOINT ["dotnet", "W3CTestApp.dll"]
