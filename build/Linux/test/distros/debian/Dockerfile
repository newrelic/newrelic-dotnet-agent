FROM mcr.microsoft.com/dotnet/sdk:6.0

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

ENV ASPNETCORE_URLS="http://+:5000"
