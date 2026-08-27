FROM rockylinux:9@sha256:d7be1c094cc5845ee815d4632fe377514ee6ebcf8efaed6892889657e5ddaaa6

WORKDIR /data

RUN dnf install --assumeyes \
        dos2unix \
        vim \
    && rpm -Uvh https://packages.microsoft.com/config/rhel/9/packages-microsoft-prod.rpm \
    && dnf install --assumeyes dotnet-sdk-10.0

RUN dotnet new mvc && \
    dotnet restore && \
    dotnet publish -c Release -o ./publish
