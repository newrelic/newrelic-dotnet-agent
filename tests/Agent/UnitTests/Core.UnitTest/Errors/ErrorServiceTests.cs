/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Errors
{
    [TestFixture]
    public class ErrorServiceTests
    {
        private IErrorService _errorService;
        private ConfigurationService _configurationService;
        private List<float> _statusCodesToIgnore;
        private List<string> _exceptionsToIgnore;
        private Dictionary<string, object> _customAttributes;

        private class ExceptionWithTypeParameter<T> : Exception
        {
        }

        [SetUp]
        public void SetUp()
        {
            _configurationService = new ConfigurationService(Mock.Create<IEnvironment>(), Mock.Create<IProcessStatic>(), Mock.Create<IHttpRuntimeStatic>(), Mock.Create<IConfigurationManagerStatic>(), Mock.Create<IDnsStatic>());
            _errorService = new ErrorService(_configurationService);
            _customAttributes = new Dictionary<string, object>() { { "custom.key", "custom.value" } };

            _statusCodesToIgnore = new List<float>() { 404, 403.1F };
            _exceptionsToIgnore = new List<string>() {
                "System.ArithmeticException",
                "NewRelic.Agent.Core.Errors.ErrorServiceTests+ExceptionWithTypeParameter"
            };
        }

        [TearDown]
        public void TearDown()
        {
            _configurationService.Dispose();
        }

        [Test]
        public void ShouldCollectErrors_MatchesErrorCollectorEnabledConfig([Values(true, false)]bool errorCollectorEnabledSetting)
        {
            SetupConfiguration(_exceptionsToIgnore, _statusCodesToIgnore, false, null, null, errorCollectorEnabled: errorCollectorEnabledSetting);

            Assert.AreEqual(errorCollectorEnabledSetting, _errorService.ShouldCollectErrors);
        }

        [Test]
        public void ShouldIgnoreException_ReturnsFalse_IfExceptionIsNotIgnored()
        {
            SetupConfiguration(_exceptionsToIgnore, _statusCodesToIgnore, false, null, null, errorCollectorEnabled: true);

            var exception = new Exception();
            Assert.IsFalse(_errorService.ShouldIgnoreException(exception));
        }

        [Test]
        public void ShouldIgnoreException_ReturnsTrue_IfExceptionIsIgnored()
        {
            SetupConfiguration(_exceptionsToIgnore, _statusCodesToIgnore, false, null, null, errorCollectorEnabled: true);

            var exception = new ArithmeticException();
            Assert.IsTrue(_errorService.ShouldIgnoreException(exception));
        }

        [Test]
        public void ShouldIgnoreException_ReturnsTrue_IfInnerExceptionIsIgnored()
        {
            SetupConfiguration(_exceptionsToIgnore, _statusCodesToIgnore, false, null, null, errorCollectorEnabled: true);

            var exception = new Exception("OuterException", new ArithmeticException("InnerException"));
            Assert.IsTrue(_errorService.ShouldIgnoreException(exception));
        }

        [Test]
        public void ShouldIgnoreException_ReturnsTrue_IfOuterExceptionIsIgnored()
        {
            SetupConfiguration(_exceptionsToIgnore, _statusCodesToIgnore, false, null, null, true);

            var exception = new ArithmeticException("OuterException", new Exception("InnerException"));
            Assert.IsTrue(_errorService.ShouldIgnoreException(exception));
        }

        [Test]
        public void ShouldIgnoreException_ReturnsTrue_IfExceptionWithTypeParameterIsIgnored()
        {
            SetupConfiguration(_exceptionsToIgnore, _statusCodesToIgnore, false, null, null, true);

            var exception = new ExceptionWithTypeParameter<string>();
            Assert.IsTrue(_errorService.ShouldIgnoreException(exception));
        }

        [TestCase(404, null, ExpectedResult = true)]
        [TestCase(404, 1, ExpectedResult = true)]
        [TestCase(403, 1, ExpectedResult = true)]
        [TestCase(403, null, ExpectedResult = false)]
        [TestCase(500, null, ExpectedResult = false)]
        public bool ShouldIgnoreHttpStatusCode_ReturnsExpectedResult(int statusCode, int? subStatusCode)
        {
            SetupConfiguration(_exceptionsToIgnore, _statusCodesToIgnore, false, null, null, true);

            return _errorService.ShouldIgnoreHttpStatusCode(statusCode, subStatusCode);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FromException_ReturnsExpectedErrorData(bool stripExceptionMessages)
        {
            SetupConfiguration(_exceptionsToIgnore, _statusCodesToIgnore, stripExceptionMessages, null, null, true);

            var exception = new Exception();
            var errorData = _errorService.FromException(exception);

            if (!stripExceptionMessages)
            {
                Assert.AreEqual(exception.Message, errorData.ErrorMessage);
                Assert.IsNotNull(errorData.StackTrace);
            }
            else
            {
                Assert.AreEqual(ErrorData.StripExceptionMessagesMessage, errorData.ErrorMessage);
                Assert.IsTrue(errorData.StackTrace.Contains(ErrorData.StripExceptionMessagesMessage));
            }

            Assert.AreEqual(exception.GetType().FullName, errorData.ErrorTypeName);
            Assert.AreEqual(DateTimeKind.Utc, errorData.NoticedAt.Kind);
            Assert.IsNull(errorData.Path);
            Assert.IsEmpty(errorData.CustomAttributes);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FromException_MarkErrorDataAsExpected_ExpectedErrorClasses(bool hasExpectedError)
        {
            var expectedClasses = new List<string>()
            {
                "System.IO.DirectoryNotFoundException",
            };

            if (hasExpectedError)
            {
                SetupConfiguration(expectedClasses, null, false, null, null, true);
            }

            var expectedExceptionRoot = new IOException("Root Exception", new DirectoryNotFoundException());
            var expectedInnterExceptionChild = expectedExceptionRoot.InnerException;

            var errorData1 = _errorService.FromException(expectedExceptionRoot);
            var errorData2 = _errorService.FromException(expectedInnterExceptionChild);

            if (hasExpectedError)
            {
                Assert.IsTrue(errorData1.IsExpected);
                Assert.IsTrue(errorData2.IsExpected);
            }
            else
            {
                Assert.IsFalse(errorData1.IsExpected);
                Assert.IsFalse(errorData2.IsExpected);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FromException_MarkErrorDataAsExpected_ExpectedErrorMessages(bool hasExpectedError)
        {
            var expectedMessages = new Dictionary<string, IEnumerable<string>>
            {
                { "System.IO.DirectoryNotFoundException", new List<string>() {"error message 1", "error message 2"} }
            };

            if (hasExpectedError)
            {
                SetupConfiguration(null, null, false, null, expectedMessages, errorCollectorEnabled: true);
            }

            var expectedException1 = new DirectoryNotFoundException("this is error message 1 ");
            var expectedException2 = new DirectoryNotFoundException("this is error message 2 ");

            var errorData1 = _errorService.FromException(expectedException1);
            var errorData2 = _errorService.FromException(expectedException2);

            if (hasExpectedError)
            {
                Assert.IsTrue(errorData1.IsExpected);
                Assert.IsTrue(errorData2.IsExpected);
            }
            else
            {
                Assert.IsFalse(errorData1.IsExpected);
                Assert.IsFalse(errorData2.IsExpected);
            }
        }

        [Test]
        public void FromException_MarkErrorDataAsExpected_SameErrorClass_In_ExpectedClasses_ExpectedErrorMessages()
        {
            var expectedMessages = new Dictionary<string, IEnumerable<string>>
            {
                { "System.IO.DirectoryNotFoundException", new List<string>() {"error message 1", "error message 2"} }
            };

            var expectedClasses = new List<string>()
            {
                "System.IO.DirectoryNotFoundException",
            };

            SetupConfiguration(null, null, false, expectedClasses, expectedMessages, true);
            var expectedException = new DirectoryNotFoundException("any error messages");

            var errorData = _errorService.FromException(expectedException);

            Assert.IsTrue(errorData.IsExpected);

        }

        private void SetupConfiguration(List<string> exceptionsToIgnore, List<float> statusCodesToIgnore,
            bool stripExceptionMessages, List<string> errorClassesToBeExpected,
            IEnumerable<KeyValuePair<string, IEnumerable<string>>> errorMessagesToBeExpected, bool errorCollectorEnabled)
        {
            var config = new configuration();

            config.errorCollector.enabled = errorCollectorEnabled;
            config.stripExceptionMessages.enabled = stripExceptionMessages;

            if (exceptionsToIgnore != null)
            {
                config.errorCollector.ignoreErrors.exception = exceptionsToIgnore;
            }

            if (statusCodesToIgnore != null)
            {
                config.errorCollector.ignoreStatusCodes.code = statusCodesToIgnore;
            }

            if (errorClassesToBeExpected != null)
            {
                config.errorCollector.expectedClasses.errorClass = errorClassesToBeExpected;
            }

            if (errorMessagesToBeExpected != null)
            {
                foreach (var errorMessage in errorMessagesToBeExpected)
                {
                    var x = new configurationErrorCollectorErrorClass()
                    {
                        name = errorMessage.Key,
                        message = errorMessage.Value.ToList()
                    };

                    config.errorCollector.expectedMessages.Add(x);
                }
            }

            EventBus<ConfigurationDeserializedEvent>.Publish(new ConfigurationDeserializedEvent(config));
        }

    }
}
