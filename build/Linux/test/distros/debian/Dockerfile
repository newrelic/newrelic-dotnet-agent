FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /data

RUN apt-get update && \
    apt-get install --assume-yes \
        libc++1 \
        dos2unix \
        vim

RUN dotnet new mvc && \
    dotnet restore && \
    dotnet build
