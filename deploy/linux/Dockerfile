FROM debian:stable-20260918@sha256:2bf225230f05881b6a36d20f484088f408c9a1309edc730c0bf01f15e635d411

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
