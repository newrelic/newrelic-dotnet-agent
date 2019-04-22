using System.Collections.Generic;
using NewRelic.Agent.Core.DataTransport;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Configuration
{
	[TestFixture, Category("Configuration")]
	public class SecurityPoliciesConfigurationTests
	{
		private static readonly SecurityPolicyState DisabledOptionalState = new SecurityPolicyState(false, false);
		private static readonly SecurityPolicyState DisabledRequiredState = new SecurityPolicyState(false, true);

		[Test]
		public void GetMissingExpectedSeverPolicyNamesShouldBeEmptyWhenAllExist()
		{
			var serverPoliciesWithExtras = new Dictionary<string, SecurityPolicyState>
			{
				{"record_sql", DisabledOptionalState},
				{"attributes_include", DisabledOptionalState},
				{"allow_raw_exception_messages", DisabledOptionalState},
				{"custom_events", DisabledOptionalState},
				{"custom_parameters", DisabledOptionalState},
				{"custom_instrumentation_editor", DisabledOptionalState},
				{"dotnet_unknown_setting", DisabledOptionalState}
			};

			var missingExpectedPolicies = SecurityPoliciesConfiguration.GetMissingExpectedSeverPolicyNames(serverPoliciesWithExtras);
			Assert.IsEmpty(missingExpectedPolicies);
		}

		[Test]
		public void GetMissingExpectedSeverPolicyNamesShouldContainMissingPolicies()
		{
			
			var serverPoliciesWithMissing = new Dictionary<string, SecurityPolicyState>
			{
				{"record_sql", DisabledOptionalState},
				{"attributes_include", DisabledOptionalState},
				{"allow_raw_exception_messages", DisabledOptionalState},
				{"custom_events", DisabledOptionalState}
			};

			var expectedMissing = new List<string> {"custom_parameters", "custom_instrumentation_editor"};

			var missingExpectedPolicies = SecurityPoliciesConfiguration.GetMissingExpectedSeverPolicyNames(serverPoliciesWithMissing);
			Assert.AreEqual(expectedMissing, missingExpectedPolicies);
		}

		[Test]
		public void GetMissingRequiredPoliciesShouldBeEmptyWhenAllRequiredKnown()
		{
			var serverPoliciesAllRequiredKnown = new Dictionary<string, SecurityPolicyState>
			{
				{"record_sql", DisabledRequiredState},
				{"attributes_include", DisabledRequiredState},
				{"allow_raw_exception_messages", DisabledRequiredState},
				{"custom_events", DisabledRequiredState},
				{"custom_parameters", DisabledRequiredState},
				{"custom_instrumentation_editor", DisabledRequiredState},
				{"dotnet_unknown_setting", DisabledOptionalState}
			};

			var missingRequiredPolicies = SecurityPoliciesConfiguration.GetMissingRequiredPolicies(serverPoliciesAllRequiredKnown);
			Assert.IsEmpty(missingRequiredPolicies);
		}

		[Test]
		public void GetMissingRequiredPoliciesShouldContainMissingRequiredPolicies()
		{
			var serverPoliciesAllRequiredKnown = new Dictionary<string, SecurityPolicyState>
			{
				{"record_sql", DisabledRequiredState},
				{"attributes_include", DisabledRequiredState},
				{"allow_raw_exception_messages", DisabledRequiredState},
				{"custom_events", DisabledRequiredState},
				{"custom_parameters", DisabledRequiredState},
				{"custom_instrumentation_editor", DisabledRequiredState},
				{"dotnet_unknown_setting", DisabledRequiredState}
			};

			var expectedMissing = new List<string> { "dotnet_unknown_setting" };

			var missingRequiredPolicies = SecurityPoliciesConfiguration.GetMissingRequiredPolicies(serverPoliciesAllRequiredKnown);
			Assert.AreEqual(expectedMissing, missingRequiredPolicies);
		}
	}
}