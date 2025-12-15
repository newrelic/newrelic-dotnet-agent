

FROM ubuntu:noble-20240605@sha256:2e863c44b718727c860746568e1d54afd13b2fa71b160f5cd9058fc436217b30

RUN apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
    rpm \
    gnupg2 \
    dos2unix \
    wget \
#to fetch New Relic's public key without certificate issues
    ca-certificates \
# to clone exiftool repository
    git \
# save on final image size
 && rm -rf /var/lib/apt/lists/*

# Install New Relic's public key to verify RPM signature
 RUN wget -O /tmp/newrelic-public.gpg https://download.newrelic.com/NEWRELIC_APT_2DAD550E.public \
 && rpm --import /tmp/newrelic-public.gpg

 # Install exiftool
RUN git clone https://github.com/exiftool/exiftool.git