

FROM ubuntu:noble-20240605@sha256:2e863c44b718727c860746568e1d54afd13b2fa71b160f5cd9058fc436217b30

RUN apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
    git ca-certificates \
    rpm \
    gnupg2 \
    dos2unix \
    dtrx \ 
# save on final image size
 && rm -rf /var/lib/apt/lists/*

# Install New Relic's public key to verify RPM signature
RUN apt-get update && apt-get install -y --no-install-recommends \
# wget to download New Relic's public key
    wget \
 && wget -O /tmp/newrelic-public.gpg https://download.newrelic.com/548C16BF.gpg \
 && rpm --import /tmp/newrelic-public.gpg \
# cleanup temporary state to save on final image size
 && apt-get autoremove -y wget \
 && rm -rf /var/lib/apt/lists/*

 # Install exiftool
RUN git clone https://github.com/exiftool/exiftool.git