FROM public.ecr.aws/lambda/dotnet:7 AS base
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:7.0-bullseye-slim as build
WORKDIR /src
COPY ["LambdaFunctionTestApp.csproj", "LambdaFunctionTestApp/"]
RUN dotnet restore "LambdaFunctionTestApp/LambdaFunctionTestApp.csproj"

WORKDIR "/src/LambdaFunctionTestApp"
COPY . .

RUN dotnet build "LambdaFunctionTestApp.csproj" --configuration Debug --output /app/build

FROM build AS publish
RUN dotnet publish "LambdaFunctionTestApp.csproj" \
            --configuration Debug \ 
            --runtime linux-x64 \
            --self-contained false \ 
            --output /app/publish \
            -p:PublishReadyToRun=true  

FROM base AS final
WORKDIR /var/task
COPY --from=publish /app/publish ${LAMBDA_TASK_ROOT}

WORKDIR /
COPY --from=build /src/LambdaFunctionTestApp/startup.sh /
RUN chmod a+x /startup.sh

ARG NEW_RELIC_LICENSE_KEY
ARG NEW_RELIC_APP_NAME

ENV CORECLR_ENABLE_PROFILING=1 \
CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
CORECLR_NEWRELIC_HOME=/usr/local/newrelic-dotnet-agent \
CORECLR_PROFILER_PATH=/usr/local/newrelic-dotnet-agent/libNewRelicProfiler.so \
NEW_RELIC_LICENSE_KEY=${NEW_RELIC_LICENSE_KEY} \
NEW_RELIC_APP_NAME=${NEW_RELIC_APP_NAME} \
NEWRELIC_LOG_DIRECTORY=/app/logs \
NEW_RELIC_SERVERLESS_MODE_ENABLED=1

# create a file that the agent can write to -- later, this should probably be an external mount
RUN touch /tmp/newrelic-telemetry && chmod 666 /tmp/newrelic-telemetry

ENTRYPOINT ["/startup.sh"]
# Set the CMD to your handler (could also be done as a parameter override outside of the Dockerfile)
CMD [ "LambdaFunctionTestApp::LambdaFunctionTestApp.Function::FunctionHandler" ]
