using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NUnit.Framework;
using Telerik.JustMock;
using Telerik.JustMock.Helpers;

namespace NewRelic.Agent.Core.BrowserMonitoring
{
	[TestFixture, Category("BrowserMonitoring")]
	public class BrowserMonitoringPrereqCheckerTests
	{
		[NotNull]
		private BrowserMonitoringPrereqChecker _checker;

		[NotNull]
		private IConfiguration _configuration;

		[NotNull]
		private ITransaction _internalTransaction;

		[NotNull]
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
				const String path = "my/path";
				const String contentType = "text/html";
				_immutableTransaction = BuildTestTransaction();

				Assert.IsTrue(_checker.ShouldAutomaticallyInject(_internalTransaction, path, contentType));
			}

			[Test]
			public void ShouldNotInjectWhenAutoInstrumentRumDisabled()
			{
				const String path = "my/path";
				const String contentType = "text/html";
				_immutableTransaction = BuildTestTransaction();
				Mock.Arrange(() => _configuration.BrowserMonitoringAutoInstrument).Returns(false);

				Assert.IsFalse(_checker.ShouldAutomaticallyInject(_internalTransaction, path, contentType));
			}

			[Test]
			public void ShouldNotInjectWhenJavascriptLoaderTypeIsNone()
			{
				const String path = "my/path";
				const String contentType = "text/html";
				_immutableTransaction = BuildTestTransaction();
				Mock.Arrange(() => _configuration.BrowserMonitoringJavaScriptAgentLoaderType).Returns("none");

				Assert.IsFalse(_checker.ShouldAutomaticallyInject(_internalTransaction, path, contentType));
			}

			[Test]
			public void ShouldNotInjectWhenTransactionSetToIgnoreAutoBrowserMonitoring()
			{
				const String path = "my/path";
				const String contentType = "text/html";
				_immutableTransaction = BuildTestTransaction(ignoreAutoBrowserMonitoring: true);

				Assert.IsFalse(_checker.ShouldAutomaticallyInject(_internalTransaction, path, contentType));
			}

			[Test]
			public void ShouldNotInjectWhenTransactionSetToIgnoreAllBrowserMonitoring()
			{
				const String path = "my/path";
				const String contentType = "text/html";
				_immutableTransaction = BuildTestTransaction(ignoreAllBrowserMonitoring: true);

				Assert.IsFalse(_checker.ShouldAutomaticallyInject(_internalTransaction, path, contentType));
			}

			[Test]
			public void ShouldNotInjectWhenNonHtmlContentType()
			{
				const String path = "my/path";
				const String contentType = "text/css";
				_immutableTransaction = BuildTestTransaction();

				Assert.IsFalse(_checker.ShouldAutomaticallyInject(_internalTransaction, path, contentType));
			}

			[Test]
			public void ShouldNotInjectWhenEmptyContentType()
			{
				const String path = "my/path";
				const String contentType = "";
				_immutableTransaction = BuildTestTransaction();

				Assert.IsFalse(_checker.ShouldAutomaticallyInject(_internalTransaction, path, contentType));
			}

			[Test]
			public void ShouldInjectWhenPathIsNullOrEmpty()
			{
				const String path = null;
				const String contentType = "text/html";
				_immutableTransaction = BuildTestTransaction();
				var exclusions = new List<Regex>
				{
					new Regex(@"Bananas/{1}?", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase)
				};

				Mock.Arrange(() => _configuration.RequestPathExclusionList).Returns(exclusions);

				Assert.IsTrue(_checker.ShouldAutomaticallyInject(_internalTransaction, path, contentType));
			}

			[Test]
			public void ShouldNotInjectWhenPathBlacklisted()
			{
				const String path = @"http://localhost/BlogEngineWeb/Posts/default.aspx";
				const String contentType = "text/html";
				_immutableTransaction = BuildTestTransaction();
				var exclusions = new List<Regex>
				{
					new Regex(@"Bananas/{1}?", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase),
					new Regex(@"Posts/{1}?", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase)
				};

				Mock.Arrange(() => _configuration.RequestPathExclusionList).Returns(exclusions);

				Assert.IsFalse(_checker.ShouldAutomaticallyInject(_internalTransaction, path, contentType));
			}

			[Test]
			public void ShouldInjectWhenPathNotBlacklisted()
			{
				const String path = @"http://localhost/BlogEngineWeb/Posts/default.aspx";
				const String contentType = "text/html";
				_immutableTransaction = BuildTestTransaction();
				var exclusions = new List<Regex>
				{
					new Regex(@"Bananas/{1}?", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase)
				};

				Mock.Arrange(() => _configuration.RequestPathExclusionList).Returns(exclusions);

				Assert.IsTrue(_checker.ShouldAutomaticallyInject(_internalTransaction, path, contentType));
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
				Assert.IsTrue(result);
			}

			[Test]
			public void ShouldInjectWhenAutoInstrumentRumDisabled()
			{
				_immutableTransaction = BuildTestTransaction();
				Mock.Arrange(() => _configuration.BrowserMonitoringAutoInstrument).Returns(false);

				var result = _checker.ShouldManuallyInject(_internalTransaction);
				Assert.IsTrue(result);
			}

			[Test]
			public void ShouldNotInjectWhenJavascriptLoaderTypeIsNone()
			{
				_immutableTransaction = BuildTestTransaction();
				Mock.Arrange(() => _configuration.BrowserMonitoringJavaScriptAgentLoaderType).Returns("none");

				var result = _checker.ShouldManuallyInject(_internalTransaction);
				Assert.IsFalse(result);
			}

			[Test]
			public void ShouldInjectWhenTransactionSetToIgnoreAutoBrowserMonitoring()
			{
				_immutableTransaction = BuildTestTransaction(ignoreAutoBrowserMonitoring: true);

				var result = _checker.ShouldManuallyInject(_internalTransaction);
				Assert.IsTrue(result);
			}

			[Test]
			public void ShouldNotInjectWhenTransactionSetToIgnoreAllBrowserMonitoring()
			{
				_immutableTransaction = BuildTestTransaction(ignoreAllBrowserMonitoring: true);

				var result = _checker.ShouldManuallyInject(_internalTransaction);

				Assert.IsFalse(result);
			}

		}

		[NotNull]
		private ImmutableTransaction BuildTestTransaction(Boolean ignoreAutoBrowserMonitoring = false, Boolean ignoreAllBrowserMonitoring = false)
		{
			var name = TransactionName.ForWebTransaction("foo", "bar");
			var segments = Enumerable.Empty<Segment>();
			var metadata = new TransactionMetadata().ConvertToImmutableMetadata();
			var guid = Guid.NewGuid().ToString();

			_internalTransaction = Mock.Create<ITransaction>();
			Mock.Arrange(() => _internalTransaction.IgnoreAllBrowserMonitoring).Returns(ignoreAllBrowserMonitoring);
			Mock.Arrange(() => _internalTransaction.IgnoreAutoBrowserMonitoring).Returns(ignoreAutoBrowserMonitoring);

			return new ImmutableTransaction(name, segments, metadata, DateTime.UtcNow, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), guid, ignoreAutoBrowserMonitoring, ignoreAllBrowserMonitoring, false, SqlObfuscator.GetObfuscatingSqlObfuscator());
		}
	}
}
