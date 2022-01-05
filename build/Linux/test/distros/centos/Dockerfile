FROM centos:7

WORKDIR /data
USER root

RUN rpm -Uvh https://packages.microsoft.com/config/centos/7/packages-microsoft-prod.rpm

RUN yum --assumeyes update
RUN yum --assumeyes install libunwind \
        libicu \
        dotnet-sdk-6.0 \
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

ENV ASPNETCORE_URLS="http://+:5000"
