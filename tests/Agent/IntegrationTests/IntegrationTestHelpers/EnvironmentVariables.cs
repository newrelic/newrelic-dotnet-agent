using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public static class EnvironmentVariables
    {
        [CanBeNull] public static readonly String DestinationWorkingDirectoryRemotePath = Environment.GetEnvironmentVariable("INTEGRATION_TEST_WORKING_DIRECTORY_DESTINATION");
        [CanBeNull] public static readonly String LicenseKey = Environment.GetEnvironmentVariable("NEWRELIC_LICENSEKEY");
        [CanBeNull] public static readonly String ApiKey = Environment.GetEnvironmentVariable("NEWRELIC_APIKEY");

    }
}
