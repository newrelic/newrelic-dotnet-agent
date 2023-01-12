# MockNewRelic

This application serves as a mock New Relic or "collector" set of endpoints for tests requiring that round trip for proper verification. 

It is a very naive implementation and not meant to serve as a proper "mock collector" in its current form.

## SSL

To support protocol 15+ this has been updated to use SSL. The app uses the self-signed development certificate installed by the .NET Core SDK.

In order for agent to be able to communicate with this mock collector successfully, the self-signed certificate must be trusted on the system by the
user the integration tests run as (which should be Administrator on a Windows system).

This requires a one-time step of running the command "dotnet dev-certs https --trust" from a command prompt or Powershell running as the same
user the integration tests are run as (again, Administrator on Windows).

For more information please see https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-dev-certs
