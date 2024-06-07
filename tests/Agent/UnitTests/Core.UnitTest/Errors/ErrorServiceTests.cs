// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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
            SetupConfiguration(_exceptionsToIgnore, null, _statusCodesToIgnore, false, null, null, null, errorCollectorEnabled: errorCollectorEnabledSetting);

            Assert.That(_errorService.ShouldCollectErrors, Is.EqualTo(errorCollectorEnabledSetting));
        }

        [Test]
        public void ShouldIgnoreException_ReturnsFalse_IfExceptionIsNotIgnored()
        {
            SetupConfiguration(_exceptionsToIgnore, null, _statusCodesToIgnore, false, null, null, null, errorCollectorEnabled: true);

            var exception = new Exception();
            Assert.That(_errorService.ShouldIgnoreException(exception), Is.False);
        }

        [Test]
        public void ShouldIgnoreException_ReturnsTrue_IfExceptionIsIgnored()
        {
            SetupConfiguration(_exceptionsToIgnore, null, _statusCodesToIgnore, false, null, null, null, errorCollectorEnabled: true);

            var exception = new ArithmeticException();
            Assert.That(_errorService.ShouldIgnoreException(exception), Is.True);
        }

        [Test]
        public void ShouldIgnoreException_ReturnsTrue_IfInnerExceptionIsIgnored()
        {
            SetupConfiguration(_exceptionsToIgnore, null, _statusCodesToIgnore, false, null, null, null, errorCollectorEnabled: true);

            var exception = new Exception("OuterException", new ArithmeticException("InnerException"));
            Assert.That(_errorService.ShouldIgnoreException(exception), Is.True);
        }

        [Test]
        public void ShouldIgnoreException_ReturnsTrue_IfOuterExceptionIsIgnored()
        {
            SetupConfiguration(_exceptionsToIgnore, null, _statusCodesToIgnore, false, null, null, null, true);

            var exception = new ArithmeticException("OuterException", new Exception("InnerException"));
            Assert.That(_errorService.ShouldIgnoreException(exception), Is.True);
        }

        [Test]
        public void ShouldIgnoreException_ReturnsTrue_IfExceptionWithTypeParameterIsIgnored()
        {
            SetupConfiguration(_exceptionsToIgnore, null, _statusCodesToIgnore, false, null, null, null, true);

            var exception = new ExceptionWithTypeParameter<string>();
            Assert.That(_errorService.ShouldIgnoreException(exception), Is.True);
        }

        [TestCase(404, null, ExpectedResult = true)]
        [TestCase(404, 1, ExpectedResult = true)]
        [TestCase(403, 1, ExpectedResult = true)]
        [TestCase(403, null, ExpectedResult = false)]
        [TestCase(500, null, ExpectedResult = false)]
        public bool ShouldIgnoreHttpStatusCode_ReturnsExpectedResult(int statusCode, int? subStatusCode)
        {
            SetupConfiguration(_exceptionsToIgnore, null, _statusCodesToIgnore, false, null, null, null, true);

            return _errorService.ShouldIgnoreHttpStatusCode(statusCode, subStatusCode);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ShouldIgnoreException_IgnoreErrorClasses(bool hasIgnoreError)
        {
            var ignoreClasses = new List<string>()
            {
                "System.IO.DirectoryNotFoundException",
            };

            if (hasIgnoreError)
            {
                SetupConfiguration(ignoreClasses, null, null, false, null, null, null, true);
            }

            var ignoreExceptionRoot = new IOException("Root Exception", new DirectoryNotFoundException());
            var ignoreInnterExceptionChild = ignoreExceptionRoot.InnerException;

            if (hasIgnoreError)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(_errorService.ShouldIgnoreException(ignoreExceptionRoot), Is.True);
                    Assert.That(_errorService.ShouldIgnoreException(ignoreInnterExceptionChild), Is.True);
                });
            }
            else
            {
                Assert.Multiple(() =>
                {
                    Assert.That(_errorService.ShouldIgnoreException(ignoreExceptionRoot), Is.False);
                    Assert.That(_errorService.ShouldIgnoreException(ignoreInnterExceptionChild), Is.False);
                });
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ShouldIgnoreException_IgnoreErrorMessages(bool hasIgnoreError)
        {
            var ignoreErrorMessages = new Dictionary<string, IEnumerable<string>>
            {
                { "System.IO.DirectoryNotFoundException", new List<string>() {"error message 1", "error message 2"} }
            };

            if (hasIgnoreError)
            {
                SetupConfiguration(null, ignoreErrorMessages, null, false, null, null, null, errorCollectorEnabled: true);
            }

            var ignoreException1 = new DirectoryNotFoundException("this is error message 1 ");
            var ignoreException2 = new DirectoryNotFoundException("this is error message 2 ");


            if (hasIgnoreError)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(_errorService.ShouldIgnoreException(ignoreException1), Is.True);
                    Assert.That(_errorService.ShouldIgnoreException(ignoreException2), Is.True);
                });
            }
            else
            {
                Assert.Multiple(() =>
                {
                    Assert.That(_errorService.ShouldIgnoreException(ignoreException1), Is.False);
                    Assert.That(_errorService.ShouldIgnoreException(ignoreException2), Is.False);
                });
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FromException_ReturnsExpectedErrorData(bool stripExceptionMessages)
        {
            SetupConfiguration(_exceptionsToIgnore, null, _statusCodesToIgnore, stripExceptionMessages, null, null, null, true);

            var exception = new Exception();
            var errorData = _errorService.FromException(exception);

            if (!stripExceptionMessages)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(errorData.ErrorMessage, Is.EqualTo(exception.Message));
                    Assert.That(errorData.StackTrace, Is.Not.Null);
                });
            }
            else
            {
                Assert.Multiple(() =>
                {
                    Assert.That(errorData.ErrorMessage, Is.EqualTo(ErrorData.StripExceptionMessagesMessage));
                    Assert.That(errorData.StackTrace, Does.Contain(ErrorData.StripExceptionMessagesMessage));
                });
            }

            Assert.Multiple(() =>
            {
                Assert.That(errorData.ErrorTypeName, Is.EqualTo(exception.GetType().FullName));
                Assert.That(errorData.NoticedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
                Assert.That(errorData.Path, Is.Null);
            });
            Assert.That(errorData.CustomAttributes, Is.Empty);
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
                SetupConfiguration(null, null, null, false, expectedClasses, null, null, true);
            }

            var expectedExceptionRoot = new IOException("Root Exception", new DirectoryNotFoundException());
            var expectedInnterExceptionChild = expectedExceptionRoot.InnerException;

            var errorData1 = _errorService.FromException(expectedExceptionRoot);
            var errorData2 = _errorService.FromException(expectedInnterExceptionChild);

            if (hasExpectedError)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(errorData1.IsExpected, Is.True);
                    Assert.That(errorData2.IsExpected, Is.True);
                });
            }
            else
            {
                Assert.Multiple(() =>
                {
                    Assert.That(errorData1.IsExpected, Is.False);
                    Assert.That(errorData2.IsExpected, Is.False);
                });
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
                SetupConfiguration(null, null, null, false, null, expectedMessages, null, errorCollectorEnabled: true);
            }

            var expectedException1 = new DirectoryNotFoundException("this is error message 1 ");
            var expectedException2 = new DirectoryNotFoundException("this is error message 2 ");

            var errorData1 = _errorService.FromException(expectedException1);
            var errorData2 = _errorService.FromException(expectedException2);

            if (hasExpectedError)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(errorData1.IsExpected, Is.True);
                    Assert.That(errorData2.IsExpected, Is.True);
                });
            }
            else
            {
                Assert.Multiple(() =>
                {
                    Assert.That(errorData1.IsExpected, Is.False);
                    Assert.That(errorData2.IsExpected, Is.False);
                });
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FromErrorHttpStatusCode_MarkErrorDataAsExpected_ExpectedStatusCodes(bool hasExpectedError)
        {

            if (hasExpectedError)
            {
                var expectedStatusCodes = "400,401,404-415";
                SetupConfiguration(null, null, null, false, null, null, expectedStatusCodes, errorCollectorEnabled: true);
            }

            var errorData1 = _errorService.FromErrorHttpStatusCode(400, null, DateTime.Now);
            var errorData2 = _errorService.FromErrorHttpStatusCode(401, 4, DateTime.Now);
            var errorData3 = _errorService.FromErrorHttpStatusCode(405, null, DateTime.Now);
            var errorData4 = _errorService.FromErrorHttpStatusCode(500, null, DateTime.Now);

            if (hasExpectedError)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(errorData1.IsExpected, Is.True);
                    Assert.That(errorData2.IsExpected, Is.True);
                    Assert.That(errorData3.IsExpected, Is.True);
                    Assert.That(errorData4.IsExpected, Is.False);
                });
            }
            else
            {
                Assert.Multiple(() =>
                {
                    Assert.That(errorData1.IsExpected, Is.False);
                    Assert.That(errorData2.IsExpected, Is.False);
                    Assert.That(errorData3.IsExpected, Is.False);
                    Assert.That(errorData4.IsExpected, Is.False);
                });
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

            SetupConfiguration(null, null, null, false, expectedClasses, expectedMessages, null, true);
            var expectedException = new DirectoryNotFoundException("any error messages");

            var errorData = _errorService.FromException(expectedException);

            Assert.That(errorData.IsExpected, Is.True);

        }

        private void SetupConfiguration(List<string> classesToBeIgnored, IEnumerable<KeyValuePair<string, IEnumerable<string>>> errorMessagesToBeIgnored,
            List<float> statusCodesToIgnore, bool stripExceptionMessages, List<string> errorClassesToBeExpected,
            IEnumerable<KeyValuePair<string, IEnumerable<string>>> errorMessagesToBeExpected, string expectedStatusCodes, bool errorCollectorEnabled)
        {
            var config = new configuration();

            config.errorCollector.enabled = errorCollectorEnabled;
            config.stripExceptionMessages.enabled = stripExceptionMessages;

            if (classesToBeIgnored != null)
            {
                config.errorCollector.ignoreClasses.errorClass = classesToBeIgnored;
            }

            if (errorMessagesToBeIgnored != null)
            {
                foreach (var errorMessage in errorMessagesToBeIgnored)
                {
                    var x = new ErrorMessagesCollectionErrorClass()
                    {
                        name = errorMessage.Key,
                        message = errorMessage.Value.ToList()
                    };

                    config.errorCollector.ignoreMessages.Add(x);
                }
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
                    var x = new ErrorMessagesCollectionErrorClass()
                    {
                        name = errorMessage.Key,
                        message = errorMessage.Value.ToList()
                    };

                    config.errorCollector.expectedMessages.Add(x);
                }
            }

            if (!string.IsNullOrEmpty(expectedStatusCodes))
            {
                config.errorCollector.expectedStatusCodes = expectedStatusCodes;
            }

            EventBus<ConfigurationDeserializedEvent>.Publish(new ConfigurationDeserializedEvent(config));
        }

        private void SetupErrorGroupCallback(Func<IReadOnlyDictionary<string, object>, string> callback)
        {
            EventBus<ErrorGroupCallbackUpdateEvent>.Publish(new ErrorGroupCallbackUpdateEvent(callback));
        }
    }
}
