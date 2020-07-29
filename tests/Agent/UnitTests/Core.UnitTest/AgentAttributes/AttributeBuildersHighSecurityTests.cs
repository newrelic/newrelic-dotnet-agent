using System;
using NUnit.Framework;

namespace NewRelic.Agent.Core.AgentAttributes
{
    [TestFixture]
    public class AttributeBuildersHighSecurityTests
    {
        [TestFixture]
        public class IncludeInHighSecurityTests
        {
            [Test]
            public void ShouldIncludeQueueWaitTimeInHighSecurityMode()
            {
                var attribute = Transactions.Attribute.BuildQueueWaitTimeAttribute(TimeSpan.FromMinutes(2));
                Assert.IsFalse(attribute.ExcludeForHighSecurity);
            }

            [Test]
            public void ShouldIncludeQueueDurationInHighSecurityMode()
            {
                var attribute = Transactions.Attribute.BuildQueueDurationAttribute(TimeSpan.FromMinutes(2));
                Assert.IsFalse(attribute.ExcludeForHighSecurity);
            }

            [Test]
            public void ShouldIncludeOriginalUrlInHighSecurityMode()
            {
                var attribute = Transactions.Attribute.BuildOriginalUrlAttribute("/url");
                Assert.IsFalse(attribute.ExcludeForHighSecurity);
            }

            [Test]
            public void ShouldIncludeRequestRefererInHighSecurityMode()
            {
                var attribute = Transactions.Attribute.BuildRequestRefererAttribute("/ref");
                Assert.IsFalse(attribute.ExcludeForHighSecurity);
            }

            [Test]
            public void ShouldIncludeResponseStatusInHighSecurityMode()
            {
                var attribute = Transactions.Attribute.BuildResponseStatusAttribute("200");
                Assert.IsFalse(attribute.ExcludeForHighSecurity);
            }

            [Test]
            public void ShouldIncludeClientCrossProcessIdInHighSecurityMode()
            {
                var attribute = Transactions.Attribute.BuildClientCrossProcessIdAttribute("cpid");
                Assert.IsFalse(attribute.ExcludeForHighSecurity);
            }

            [Test]
            public void ShouldIncludeCatTripIdInHighSecurityMode()
            {
                var attributes = Transactions.Attribute.BuildCatTripIdAttribute("tripid");
                foreach (var attribute in attributes)
                {
                    Assert.IsFalse(attribute.ExcludeForHighSecurity);
                }
            }

            [Test]
            public void ShouldIncludeCatPathHashInHighSecurityMode()
            {
                var attributes = Transactions.Attribute.BuildCatPathHash("pathhash");
                foreach (var attribute in attributes)
                {
                    Assert.IsFalse(attribute.ExcludeForHighSecurity);
                }
            }


            [Test]
            public void ShouldIncludeCatReferringPathInHighSecurityMode()
            {
                var attributes = Transactions.Attribute.BuildCatReferringPathHash("catrefpathhash");
                foreach (var attribute in attributes)
                {
                    Assert.IsFalse(attribute.ExcludeForHighSecurity);
                }
            }

            [Test]
            public void ShouldIncludeCatReferringTransactionGuidInHighSecurityMode()
            {
                var attributes = Transactions.Attribute.BuildCatReferringTransactionGuidAttribute("{GUID}");
                foreach (var attribute in attributes)
                {
                    Assert.IsFalse(attribute.ExcludeForHighSecurity);
                }
            }

            [Test]
            public void ShouldIncludeCatAlternatePathHashesInHighSecurityMode()
            {
                var attributes = Transactions.Attribute.BuildCatAlternatePathHashes("alternatepathhash");
                foreach (var attribute in attributes)
                {
                    Assert.IsFalse(attribute.ExcludeForHighSecurity);
                }
            }

            [Test]
            public void ShouldIncludeErrorTypeInHighSecurityMode()
            {
                var attribute = Transactions.Attribute.BuildErrorTypeAttribute("errorType");
                Assert.IsFalse(attribute.ExcludeForHighSecurity);
            }

            [Test]
            public void ShouldIncludeTransactionEventTypeInHighSecurityMode()
            {
                var attribute = Transactions.Attribute.BuildTypeAttribute(TypeAttributeValue.Transaction);
                Assert.IsFalse(attribute.ExcludeForHighSecurity);
            }

            [Test]
            public void ShouldIncludeTimeStampInHighSecurityMode()
            {
                var attribute = Transactions.Attribute.BuildTimeStampAttribute(DateTime.Now);
                Assert.IsFalse(attribute.ExcludeForHighSecurity);
            }

            [Test]
            public void ShouldIncludeTransactionNameInHighSecurityMode()
            {
                var attributes = Transactions.Attribute.BuildTransactionNameAttribute("transactionName");

                foreach (var attribute in attributes)
                {
                    Assert.IsFalse(attribute.ExcludeForHighSecurity);
                }
            }

            [Test]
            public void ShouldIncludeGuidInHighSecurityMode()
            {
                var attribute = Transactions.Attribute.BuildGuidAttribute("{GUID}");
                Assert.IsFalse(attribute.ExcludeForHighSecurity);
            }

            [Test]
            public void ShouldIncludeSyntheticsResourceIdAttributesInHighSecurityMode()
            {
                var attributes = Transactions.Attribute.BuildSyntheticsResourceIdAttributes("synthResourceId");
                foreach (var attribute in attributes)
                {
                    Assert.IsFalse(attribute.ExcludeForHighSecurity);
                }
            }

            [Test]
            public void ShouldIncludeSyntheticsJobIdAttributesInHighSecurityMode()
            {
                var attributes = Transactions.Attribute.BuildSyntheticsJobIdAttributes("synthJobId");
                foreach (var attribute in attributes)
                {
                    Assert.IsFalse(attribute.ExcludeForHighSecurity);
                }
            }

            [Test]
            public void ShouldIncludeSyntheticsMonitorIdAttributesInHighSecurityMode()
            {
                var attributes = Transactions.Attribute.BuildSyntheticsMonitorIdAttributes("synthMonitorId");
                foreach (var attribute in attributes)
                {
                    Assert.IsFalse(attribute.ExcludeForHighSecurity);
                }
            }

            [Test]
            public void ShouldIncludeDurationInHighSecurityMode()
            {
                var attribute = Transactions.Attribute.BuildDurationAttribute(TimeSpan.FromMinutes(2));
                Assert.IsFalse(attribute.ExcludeForHighSecurity);
            }

            [Test]
            public void ShouldIncludeWebDurationInHighSecurityMode()
            {
                var attribute = Transactions.Attribute.BuildWebDurationAttribute(TimeSpan.FromMinutes(2));
                Assert.IsFalse(attribute.ExcludeForHighSecurity);
            }

            [Test]
            public void ShouldIncludeTotalTimeInHighSecurityMode()
            {
                var attribute = Transactions.Attribute.BuildTotalTime(TimeSpan.FromMinutes(2));
                Assert.IsFalse(attribute.ExcludeForHighSecurity);
            }

            [Test]
            public void ShouldIncludeCpuTimeInHighSecurityMode()
            {
                var attribute = Transactions.Attribute.BuildCpuTime(TimeSpan.FromMinutes(2));
                Assert.IsFalse(attribute.ExcludeForHighSecurity);
            }

            [Test]
            public void ShouldIncludePerfZoneInHighSecurityMode()
            {
                var attribute = Transactions.Attribute.BuildApdexPerfZoneAttribute("PerfZone");
                Assert.IsFalse(attribute.ExcludeForHighSecurity);
            }

            [Test]
            public void ShouldIncludeExternalDurationInHighSecurityMode()
            {
                var attribute = Transactions.Attribute.BuildExternalDurationAttribute((float)TimeSpan.FromMinutes(2).TotalSeconds);
                Assert.IsFalse(attribute.ExcludeForHighSecurity);
            }

            [Test]
            public void ShouldIncludeDatabaseDurationInHighSecurityMode()
            {
                var attribute = Transactions.Attribute.BuildDatabaseDurationAttribute((float)TimeSpan.FromMinutes(2).TotalSeconds);
                Assert.IsFalse(attribute.ExcludeForHighSecurity);
            }

            [Test]
            public void ShouldIncludeDatabaseCallcountInHighSecurityMode()
            {
                var attribute = Transactions.Attribute.BuildDatabaseCallCountAttribute(10);
                Assert.IsFalse(attribute.ExcludeForHighSecurity);
            }

            [Test]
            public void ShouldIncludeErrorClassInHighSecurityMode()
            {
                var attribute = Transactions.Attribute.BuildErrorClassAttribute("DivideByZeroException");
                Assert.IsFalse(attribute.ExcludeForHighSecurity);
            }

            [Test]
            public void ShouldIncludeExternalCallCountInHighSecurityMode()
            {
                var attribute = Transactions.Attribute.BuildExternalCallCountAttribute(10);
                Assert.IsFalse(attribute.ExcludeForHighSecurity);
            }

        }

    }

    [TestFixture]
    public class ExcludeInHighSecurityTests
    {
        [Test]
        public void ShouldExcludeRequestParameterInHighSecurityMode()
        {
            var attribute = Transactions.Attribute.BuildRequestParameterAttribute("key", "value");
            Assert.IsTrue(attribute.ExcludeForHighSecurity);
        }

        [Test]
        public void ShouldExcludeServiceRequestInHighSecurityMode()
        {
            var attribute = Transactions.Attribute.BuildServiceRequestAttribute("key", "value");
            Assert.IsTrue(attribute.ExcludeForHighSecurity);
        }

        [Test]
        public void ShouldExcludeCustomErrorInHighSecurityMode()
        {
            var attribute = Transactions.Attribute.BuildCustomErrorAttribute("key", "value");
            Assert.IsTrue(attribute.ExcludeForHighSecurity);
        }

        [Test]
        public void ShouldExcludeErrorMessageInHighSecurityMode()
        {
            var attribute = Transactions.Attribute.BuildErrorMessageAttribute("errorMessage");
            Assert.IsTrue(attribute.ExcludeForHighSecurity);
        }

        [Test]
        public void ShouldExcludeCustomAttributeInHighSecurityMode()
        {
            var attribute = Transactions.Attribute.BuildCustomAttribute("key", "value");
            Assert.IsTrue(attribute.ExcludeForHighSecurity);
        }

        [Test]
        public void ShouldExcludeRequestUsernameInHighSecurityMode()
        {
            var attribute = Transactions.Attribute.BuildRequestUsernameAttribute("username");
            Assert.IsTrue(attribute.ExcludeForHighSecurity);
        }
    }
}
