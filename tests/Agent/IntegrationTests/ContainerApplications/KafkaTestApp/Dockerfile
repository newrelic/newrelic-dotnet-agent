ARG DISTRO_TAG
FROM --platform=amd64 mcr.microsoft.com/dotnet/aspnet:7.0-bullseye-slim AS base
WORKDIR /app
EXPOSE 80
RUN apt-get update \
    && apt-get install -y wget \
    && wget https://packages.microsoft.com/config/debian/11/packages-microsoft-prod.deb -O packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y aspnetcore-runtime-6.0


# build image is always amd64 (to match the runner architecture), even though the target architecture may be arm64
FROM --platform=amd64 mcr.microsoft.com/dotnet/sdk:7.0-bullseye-slim AS build
RUN apt-get update \
    && apt-get install -y wget \
    && wget https://packages.microsoft.com/config/debian/11/packages-microsoft-prod.deb -O packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y dotnet-sdk-6.0
WORKDIR /src
COPY ["KafkaTestApp/KafkaTestApp.csproj", "KafkaTestApp/"]
ARG APP_DOTNET_VERSION
RUN dotnet restore "KafkaTestApp/KafkaTestApp.csproj" -p:TargetFramework=net${APP_DOTNET_VERSION}

COPY . .
WORKDIR "/src/KafkaTestApp"
RUN dotnet build "KafkaTestApp.csproj" -c Release -o /app/build --os linux --framework net${APP_DOTNET_VERSION}


FROM build AS publish
ARG APP_DOTNET_VERSION
RUN dotnet publish "KafkaTestApp.csproj" -c Release -o /app/publish /p:UseAppHost=false --os linux --framework net${APP_DOTNET_VERSION}


FROM base AS final

# Enable the agent
ARG NEW_RELIC_HOST
ARG NEW_RELIC_LICENSE_KEY
ARG NEW_RELIC_APP_NAME

ENV CORECLR_ENABLE_PROFILING=1 \
CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
CORECLR_NEWRELIC_HOME=/usr/local/newrelic-dotnet-agent \
CORECLR_PROFILER_PATH=/usr/local/newrelic-dotnet-agent/libNewRelicProfiler.so \
NEW_RELIC_HOST=${NEW_RELIC_HOST} \
NEW_RELIC_LICENSE_KEY=${NEW_RELIC_LICENSE_KEY} \
NEW_RELIC_APP_NAME=${NEW_RELIC_APP_NAME} \
NEWRELIC_LOG_DIRECTORY=/app/logs

WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "KafkaTestApp.dll"]
