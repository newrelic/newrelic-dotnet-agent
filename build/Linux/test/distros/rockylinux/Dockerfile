FROM rockylinux/rockylinux:9.8.20260525.0@sha256:8101994123cf3d0a8fee517bee7f39e555c7d92bd2d9eb3303cc988a0eeed00f

WORKDIR /data

RUN dnf install --assumeyes \
        dos2unix \
        vim \
    && rpm -Uvh https://packages.microsoft.com/config/rhel/9/packages-microsoft-prod.rpm \
    && dnf install --assumeyes dotnet-sdk-10.0

RUN dotnet new mvc && \
    dotnet restore && \
    dotnet publish -c Release -o ./publish
