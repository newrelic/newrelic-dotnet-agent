using System;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public static class EnvironmentVariables
    {
        public static readonly string DestinationWorkingDirectoryRemotePath = Environment.GetEnvironmentVariable("INTEGRATION_TEST_WORKING_DIRECTORY_DESTINATION");
        public static readonly string LicenseKey = Environment.GetEnvironmentVariable("NEWRELIC_LICENSEKEY");
        public static readonly string ApiKey = Environment.GetEnvironmentVariable("NEWRELIC_APIKEY");

    }
}
