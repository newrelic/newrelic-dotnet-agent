using System;
using NUnit.Framework;
using NewRelic.Agent.Core.UnitTest;

namespace NewRelic.Agent.Core.Config
{
	[TestFixture]
	public class BootstrapConfigTest
	{
		[Test]
		public void TestInvalidServiceAttribute ()
		{
			using (var logging = new UnitTest.Fixtures.Logging())
			{
				// this should load with an error in the event log
				ConfigurationLoader.InitializeFromXml(
					"<configuration xmlns=\"urn:newrelic-config\">" +
					"<service bogus=\"true\" licenseKey=\"dude\"/>" +
					"<application><name>My App</name></application>" +
					"</configuration>");
				var errorMessage = Type.GetType("Mono.Runtime") == null ?
						"An error occurred parsing newrelic.config - The 'bogus' attribute is not declared." :
						"An error occurred parsing newrelic.config - XmlSchema error: Attribute declaration was not found for bogus";
				Assert.IsTrue(logging.HasMessageThatContains(errorMessage));
			}
		}
	}
}
