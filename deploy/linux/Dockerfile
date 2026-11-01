FROM debian:stable-20261027@sha256:23ef8ad09b87c0dbee73f1f962ee78a6515842480a4444a2e5275912d4cad8e4

RUN apt-get update && apt-get install -y \
    apt-utils \
    dpkg-dev \
    createrepo-c \
    awscli \
    curl \
    dos2unix \
    bsdmainutils \
    rsync \
    gnupg2 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /data

COPY deploy.bash .

COPY ./deploy_scripts /data/deploy_scripts

COPY ./packages /packages

RUN dos2unix deploy.bash && chmod a+x deploy.bash
RUN find /data/deploy_scripts -type f |xargs dos2unix
RUN find /data/deploy_scripts -name "*.bash" |xargs chmod a+x
