FROM rockylinux/rockylinux:9.8.20260525.0@sha256:8101994123cf3d0a8fee517bee7f39e555c7d92bd2d9eb3303cc988a0eeed00f

RUN curl -o /etc/yum.repos.d/newrelic-dotnet-agent.repo https://download.newrelic.com/dot_net_agent/yum/newrelic-dotnet-agent.repo

COPY --chmod=777 check-version.sh /tmp/

ENTRYPOINT ["/tmp/check-version.sh"]
