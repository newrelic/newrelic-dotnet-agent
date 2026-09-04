FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c

WORKDIR /data

RUN apt-get update && \
    apt-get install --assume-yes \
        libc++1 \
        dos2unix \
        vim

RUN dotnet new mvc && \
    dotnet restore && \
    dotnet publish -c Release -o ./publish
