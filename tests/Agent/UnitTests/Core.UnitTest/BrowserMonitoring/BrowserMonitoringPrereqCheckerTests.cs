// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Transactions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.BrowserMonitoring
{
    [TestFixture, Category("BrowserMonitoring")]
    public class BrowserMonitoringPrereqCheckerTests
    {
        private BrowserMonitoringPrereqChecker _checker;

        private IConfiguration _configuration;

        private IInternalTransaction _internalTransaction;

        private ImmutableTransaction _immutableTransaction;

        [SetUp]
        public void SetUp()
        {
            _configuration = Mock.Create<IConfiguration>();
            var configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => configurationService.Configuration).Returns(_configuration);

            _checker = new BrowserMonitoringPrereqChecker(configurationService);

            Mock.Arrange(() => _configuration.BrowserMonitoringAutoInstrument).Returns(true);
            Mock.Arrange(() => _configuration.BrowserMonitoringJavaScriptAgentLoaderType).Returns("Something");
            Mock.Arrange(() => _configuration.RequestPathExclusionList).Returns(Enumerable.Empty<Regex>());
        }

        [TestFixture]
        public class ShouldAutomaticallyInject : BrowserMonitoringPrereqCheckerTests
        {
            [Test]
            public void ShouldInjectWhenDefaultSettings()
            {
                const string path = "my/path";
                const string contentType = "text/html";
                _immutableTransaction = BuildTestTransaction();

                Assert.That(_checker.ShouldAutomaticallyInject(_internalTransaction, path, contentType), Is.True);
            }

            [Test]
            public void ShouldNotInjectWhenAutoInstrumentRumDisabled()
            {
                const string path = "my/path";
                const string contentType = "text/html";
                _immutableTransaction = BuildTestTransaction();
                Mock.Arrange(() => _configuration.BrowserMonitoringAutoInstrument).Returns(false);

                Assert.That(_checker.ShouldAutomaticallyInject(_internalTransaction, path, contentType), Is.False);
            }

            [Test]
            public void ShouldNotInjectWhenJavascriptLoaderTypeIsNone()
            {
                const string path = "my/path";
                const string contentType = "text/html";
                _immutableTransaction = BuildTestTransaction();
                Mock.Arrange(() => _configuration.BrowserMonitoringJavaScriptAgentLoaderType).Returns("none");

                Assert.That(_checker.ShouldAutomaticallyInject(_internalTransaction, path, contentType), Is.False);
            }

            [Test]
            public void ShouldNotInjectWhenTransactionSetToIgnoreAutoBrowserMonitoring()
            {
                const string path = "my/path";
                const string contentType = "text/html";
                _immutableTransaction = BuildTestTransaction(ignoreAutoBrowserMonitoring: true);

                Assert.That(_checker.ShouldAutomaticallyInject(_internalTransaction, path, contentType), Is.False);
            }

            [Test]
            public void ShouldNotInjectWhenTransactionSetToIgnoreAllBrowserMonitoring()
            {
                const string path = "my/path";
                const string contentType = "text/html";
                _immutableTransaction = BuildTestTransaction(ignoreAllBrowserMonitoring: true);

                Assert.That(_checker.ShouldAutomaticallyInject(_internalTransaction, path, contentType), Is.False);
            }

            [Test]
            public void ShouldNotInjectWhenNonHtmlContentType()
            {
                const string path = "my/path";
                const string contentType = "text/css";
                _immutableTransaction = BuildTestTransaction();

                Assert.That(_checker.ShouldAutomaticallyInject(_internalTransaction, path, contentType), Is.False);
            }

            [Test]
            public void ShouldNotInjectWhenEmptyContentType()
            {
                const string path = "my/path";
                const string contentType = "";
                _immutableTransaction = BuildTestTransaction();

                Assert.That(_checker.ShouldAutomaticallyInject(_internalTransaction, path, contentType), Is.False);
            }

            [Test]
            public void ShouldInjectWhenPathIsNullOrEmpty()
            {
                const string path = null;
                const string contentType = "text/html";
                _immutableTransaction = BuildTestTransaction();
                var exclusions = new List<Regex>
                {
                    new Regex(@"Bananas/{1}?", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase)
                };

                Mock.Arrange(() => _configuration.RequestPathExclusionList).Returns(exclusions);

                Assert.That(_checker.ShouldAutomaticallyInject(_internalTransaction, path, contentType), Is.True);
            }

            [Test]
            public void ShouldNotInjectWhenPathBlacklisted()
            {
                const string path = @"http://localhost/BlogEngineWeb/Posts/default.aspx";
                const string contentType = "text/html";
                _immutableTransaction = BuildTestTransaction();
                var exclusions = new List<Regex>
                {
                    new Regex(@"Bananas/{1}?", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase),
                    new Regex(@"Posts/{1}?", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase)
                };

                Mock.Arrange(() => _configuration.RequestPathExclusionList).Returns(exclusions);

                Assert.That(_checker.ShouldAutomaticallyInject(_internalTransaction, path, contentType), Is.False);
            }

            [Test]
            public void ShouldInjectWhenPathNotBlacklisted()
            {
                const string path = @"http://localhost/BlogEngineWeb/Posts/default.aspx";
                const string contentType = "text/html";
                _immutableTransaction = BuildTestTransaction();
                var exclusions = new List<Regex>
                {
                    new Regex(@"Bananas/{1}?", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase)
                };

                Mock.Arrange(() => _configuration.RequestPathExclusionList).Returns(exclusions);

                Assert.That(_checker.ShouldAutomaticallyInject(_internalTransaction, path, contentType), Is.True);
            }
        }

        [TestFixture]
        public class ShouldManuallyInject : BrowserMonitoringPrereqCheckerTests
        {
            [Test]
            public void ShouldInjectWhenDefaultSettings()
            {
                _immutableTransaction = BuildTestTransaction();

                var result = _checker.ShouldManuallyInject(_internalTransaction);
                Assert.That(result, Is.True);
            }

            [Test]
            public void ShouldInjectWhenAutoInstrumentRumDisabled()
            {
                _immutableTransaction = BuildTestTransaction();
                Mock.Arrange(() => _configuration.BrowserMonitoringAutoInstrument).Returns(false);

                var result = _checker.ShouldManuallyInject(_internalTransaction);
                Assert.That(result, Is.True);
            }

            [Test]
            public void ShouldNotInjectWhenJavascriptLoaderTypeIsNone()
            {
                _immutableTransaction = BuildTestTransaction();
                Mock.Arrange(() => _configuration.BrowserMonitoringJavaScriptAgentLoaderType).Returns("none");

                var result = _checker.ShouldManuallyInject(_internalTransaction);
                Assert.That(result, Is.False);
            }

            [Test]
            public void ShouldInjectWhenTransactionSetToIgnoreAutoBrowserMonitoring()
            {
                _immutableTransaction = BuildTestTransaction(ignoreAutoBrowserMonitoring: true);

                var result = _checker.ShouldManuallyInject(_internalTransaction);
                Assert.That(result, Is.True);
            }

            [Test]
            public void ShouldNotInjectWhenTransactionSetToIgnoreAllBrowserMonitoring()
            {
                _immutableTransaction = BuildTestTransaction(ignoreAllBrowserMonitoring: true);

                var result = _checker.ShouldManuallyInject(_internalTransaction);

                Assert.That(result, Is.False);
            }

        }

        private ImmutableTransaction BuildTestTransaction(bool ignoreAutoBrowserMonitoring = false, bool ignoreAllBrowserMonitoring = false)
        {
            var name = TransactionName.ForWebTransaction("foo", "bar");
            var segments = Enumerable.Empty<Segment>();
            var metadata = new TransactionMetadata("transactionGuid").ConvertToImmutableMetadata();
            var guid = Guid.NewGuid().ToString();

            _internalTransaction = Mock.Create<IInternalTransaction>();
            Mock.Arrange(() => _internalTransaction.IgnoreAllBrowserMonitoring).Returns(ignoreAllBrowserMonitoring);
            Mock.Arrange(() => _internalTransaction.IgnoreAutoBrowserMonitoring).Returns(ignoreAutoBrowserMonitoring);

            var attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
            return new ImmutableTransaction(name, segments, metadata, DateTime.UtcNow, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), guid, ignoreAutoBrowserMonitoring, ignoreAllBrowserMonitoring, false, 0.5f, false, string.Empty, null, attribDefSvc.AttributeDefs);
        }
    }
}
