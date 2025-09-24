FROM debian:bullseye-20250908@sha256:f58b816c2acc96e3e1dfe6b6c166c3d52b5541571ac4fa72a64a0bb5beaf25a3

RUN apt-get update && apt-get install -y curl gnupg \
    && curl -fsSL https://download.newrelic.com/NEWRELIC_APT_2DAD550E.public | gpg --dearmor > /usr/share/keyrings/newrelic-apt.gpg

COPY --chmod=777 check-version.sh /tmp/

ENTRYPOINT ["/tmp/check-version.sh"]
