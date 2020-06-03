# Simple smoke test

This has initially been put in place for https://newrelic.atlassian.net/browse/DOTNET-4230 (needing to test releases in production, staging and EU).

There is a .NET Core console app in 'app'.  The Docker/docker-compose assets set this up to run in Linux.  Whatever .deb package gets put in 'agent' will be installed and attached to the test app, which runs for five minutes to generate transactions.

Run this like:
`docker-compose build`
`docker-compose run -e NEW_RELIC_HOST=$COLLECTOR_HOSTNAME -e NEW_RELIC_LICENSE_KEY=$LICENSE_KEY -e NEW_RELIC_APP_NAME=DotnetReleaseTest smoketest`

List of accounts used for testing

Staging - https://staging.newrelic.com/accounts/273070/applications
US Prod - https://rpm.newrelic.com/accounts/33/applications
EU Prod - https://rpm.eu.newrelic.com/accounts/2280439/applications

