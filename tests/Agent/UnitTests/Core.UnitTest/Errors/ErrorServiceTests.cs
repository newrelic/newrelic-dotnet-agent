using System;
using System.Collections.Generic;
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
        public void ShouldIgnoreException_ReturnsFalse_IfExceptionIsNotIgnored()
        {
            SetupConfiguration(_exceptionsToIgnore, _statusCodesToIgnore, false);

            var exception = new Exception();
            Assert.IsFalse(_errorService.ShouldIgnoreException(exception));
        }

        [Test]
        public void ShouldIgnoreException_ReturnsTrue_IfExceptionIsIgnored()
        {
            SetupConfiguration(_exceptionsToIgnore, _statusCodesToIgnore, false);

            var exception = new ArithmeticException();
            Assert.IsTrue(_errorService.ShouldIgnoreException(exception));
        }

        [Test]
        public void ShouldIgnoreException_ReturnsTrue_IfInnerExceptionIsIgnored()
        {
            SetupConfiguration(_exceptionsToIgnore, _statusCodesToIgnore, false);

            var exception = new Exception("OuterException", new ArithmeticException("InnerException"));
            Assert.IsTrue(_errorService.ShouldIgnoreException(exception));
        }

        [Test]
        public void ShouldIgnoreException_ReturnsTrue_IfOuterExceptionIsIgnored()
        {
            SetupConfiguration(_exceptionsToIgnore, _statusCodesToIgnore, false);

            var exception = new ArithmeticException("OuterException", new Exception("InnerException"));
            Assert.IsTrue(_errorService.ShouldIgnoreException(exception));
        }

        [Test]
        public void ShouldIgnoreException_ReturnsTrue_IfExceptionWithTypeParameterIsIgnored()
        {
            SetupConfiguration(_exceptionsToIgnore, _statusCodesToIgnore, false);

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
            SetupConfiguration(_exceptionsToIgnore, _statusCodesToIgnore, false);

            return _errorService.ShouldIgnoreHttpStatusCode(statusCode, subStatusCode);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FromException_ReturnsExpectedErrorData(bool stripExceptionMessages)
        {
            SetupConfiguration(_exceptionsToIgnore, _statusCodesToIgnore, stripExceptionMessages);

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

        private void SetupConfiguration(List<string> exceptionsToIgnore, List<float> statusCodesToIgnore, bool stripExceptionMessages)
        {
            var config = new configuration();
            config.errorCollector.ignoreErrors.exception = exceptionsToIgnore;
            config.errorCollector.ignoreStatusCodes.code = statusCodesToIgnore;
            config.stripExceptionMessages.enabled = stripExceptionMessages;
            EventBus<ConfigurationDeserializedEvent>.Publish(new ConfigurationDeserializedEvent(config));
        }
    }
}
