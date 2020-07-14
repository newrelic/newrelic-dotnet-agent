FROM centos:7

WORKDIR /data
USER root

RUN rpm --import https://packages.microsoft.com/keys/microsoft.asc
RUN sh -c 'echo -e "[packages-microsoft-com-prod]\nname=packages-microsoft-com-prod \nbaseurl=https://packages.microsoft.com/yumrepos/microsoft-rhel7.3-prod\nenabled=1\ngpgcheck=1\ngpgkey=https://packages.microsoft.com/keys/microsoft.asc" > /etc/yum.repos.d/dotnetdev.repo'

RUN yum --assumeyes update
RUN yum --assumeyes install libunwind \
        libicu \
        dotnet-sdk-2.0.0 \
        dos2unix \
        vim \
        wget

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