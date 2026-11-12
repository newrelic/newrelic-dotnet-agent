FROM debian:stable-20261104@sha256:3f0347f4b50e7a54a91d1beaf6ce6fa5aec392347da6a9184b5bfe4e1b814538

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
