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

COPY ./AwsSdkTestApp ./ContainerApplications/AwsSdkTestApp
RUN dotnet restore "ContainerApplications/AwsSdkTestApp/AwsSdkTestApp.csproj"  -a ${TARGET_ARCH}

WORKDIR "/src/ContainerApplications/AwsSdkTestApp"
RUN dotnet build "AwsSdkTestApp.csproj" -c Release -o /app/build --os linux -a ${TARGET_ARCH}

FROM build AS publish
RUN dotnet publish "AwsSdkTestApp.csproj" -c Release -o /app/publish /p:UseAppHost=false --os linux -a ${TARGET_ARCH}

FROM base AS final

# Enable the agent
ARG NEW_RELIC_HOST
ARG NEW_RELIC_LICENSE_KEY
ARG NEW_RELIC_APP_NAME

# Control whether or not 'empty' things (e.g. message attributes) are initialized
# to an empty collection or left null
ARG AWSSDK_INITCOLLECTIONS

ENV CORECLR_ENABLE_PROFILING=1 \
CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
CORECLR_NEW_RELIC_HOME=/usr/local/newrelic-dotnet-agent \
CORECLR_PROFILER_PATH=/usr/local/newrelic-dotnet-agent/libNewRelicProfiler.so \
NEW_RELIC_HOST=${NEW_RELIC_HOST} \
NEW_RELIC_LICENSE_KEY=${NEW_RELIC_LICENSE_KEY} \
NEW_RELIC_APP_NAME=${NEW_RELIC_APP_NAME} \
NEW_RELIC_LOG_DIRECTORY=/app/logs \
AWSSDK_INITCOLLECTIONS=${AWSSDK_INITCOLLECTIONS}

WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "AwsSdkTestApp.dll"]
