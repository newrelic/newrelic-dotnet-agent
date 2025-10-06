FROM debian:trixie-20250908@sha256:833c135acfe9521d7a0035a296076f98c182c542a2b6b5a0fd7063d355d696be

RUN apt-get update && apt-get install -y curl gnupg \
    && curl -fsSL https://download.newrelic.com/NEWRELIC_APT_2DAD550E.public | gpg --dearmor > /usr/share/keyrings/newrelic-apt.gpg

COPY --chmod=777 check-version.sh /tmp/

ENTRYPOINT ["/tmp/check-version.sh"]
