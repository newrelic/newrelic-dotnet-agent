FROM rockylinux/rockylinux:10.2.20260525.0@sha256:827d37bc128288ccf160ee318bb3cb92d591164cb217e92f8bc61e3982ae1834

WORKDIR /data

RUN dnf install --assumeyes \
        dos2unix \
        vim \
    && rpm -Uvh https://packages.microsoft.com/config/rhel/10/packages-microsoft-prod.rpm \
    && dnf install --assumeyes dotnet-sdk-10.0

RUN dotnet new mvc && \
    dotnet restore && \
    dotnet publish -c Release -o ./publish
