FROM rockylinux:9

WORKDIR /data

RUN dnf install --assumeyes \
        dos2unix \
        vim \
    && rpm -Uvh https://packages.microsoft.com/config/rhel/9/packages-microsoft-prod.rpm \
    && dnf install --assumeyes dotnet-sdk-10.0

RUN dotnet new mvc && \
    dotnet restore && \
    dotnet build
