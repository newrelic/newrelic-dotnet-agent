using NUnit.Framework;

namespace NewRelic.Agent.Core
{
    [TestFixture]
    class AgentInstallConfigurationTests
    {
        [Test]
        public void AgentVersionTimeStampIsGreaterThanZero()
        {
            Assert.Greater(AgentInstallConfiguration.AgentVersionTimestamp, 0);
        }
    }
}
