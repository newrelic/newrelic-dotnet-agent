using System;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public static class EnvironmentVariables
    {
        public static readonly String DestinationWorkingDirectoryRemotePath = Environment.GetEnvironmentVariable("INTEGRATION_TEST_WORKING_DIRECTORY_DESTINATION");
        public static readonly String LicenseKey = Environment.GetEnvironmentVariable("NEWRELIC_LICENSEKEY");
        public static readonly String ApiKey = Environment.GetEnvironmentVariable("NEWRELIC_APIKEY");

    }
}
