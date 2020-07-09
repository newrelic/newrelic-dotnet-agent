FROM microsoft/dotnet:2.0.0-sdk-2.0.2-stretch

WORKDIR /data

RUN apt-get update && \
    apt-get install --assume-yes \
        libc++1 \
        dos2unix \
        vim

RUN dotnet new console && \
    dotnet restore && \
    dotnet build

RUN mkdir -p /container_apps/mvc

WORKDIR /container_apps/mvc

RUN dotnet new mvc && \
    dotnet restore && \
    dotnet build

ENV NEW_RELIC_HOST=staging-collector.newrelic.com
ENV NEW_RELIC_LICENSE_KEY="b25fd3ca20fe323a9a7c4a092e48d62dc64cc61d"