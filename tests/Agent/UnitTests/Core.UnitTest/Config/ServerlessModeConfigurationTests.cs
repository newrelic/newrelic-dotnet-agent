// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Config
{
    [TestFixture]
    [TestOf(typeof(configuration))]
    public class ServerlessModeConfigurationTests
    {
        private IEnvironment _originalEnvironment;
        private Dictionary<string, string> _envVars = new Dictionary<string, string>();

        private void SetEnvironmentVar(string name, string value)
        {
            _envVars[name] = value;
        }

        private void ClearEnvironmentVars() => _envVars.Clear();

        private string MockGetEnvironmentVar(string name)
        {
            if (_envVars.TryGetValue(name, out var value)) return value;
            return null;
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _originalEnvironment = ConfigLoaderHelpers.EnvironmentVariableProxy;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            ConfigLoaderHelpers.EnvironmentVariableProxy = _originalEnvironment;
        }

        [SetUp]
        public void Setup()
        {
            // A new environment mock needs to be created for each test to work around a weird
            // problem where the mock does not behave as expected when all of the tests are run
            // together.
            var environmentMock = Mock.Create<IEnvironment>();
            Mock.Arrange(() => environmentMock.GetEnvironmentVariable(Arg.IsAny<string>())).Returns(MockGetEnvironmentVar);
            ConfigLoaderHelpers.EnvironmentVariableProxy = environmentMock;

            ClearEnvironmentVars();
        }


        [Test]
        public void ServerlessModeEnabled_When_ServerlessEnvVarSet_LambdaFuncEnvVarNotSet_ConfigHasNoSetting()
        {
            // Arrange
            SetEnvironmentVar("NEW_RELIC_SERVERLESS_MODE_ENABLED", "true");

            var xml =
                "<configuration xmlns=\"urn:newrelic-config\"  >" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "</configuration>";


            var configuration = CreateBootstrapConfiguration(xml);

            // Act
            var result = configuration.ServerlessModeEnabled;

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void ServerlessModeEnabled_When_ServerlessEnvVarNotSet_LambdaFuncEnvVarSet_ConfigHasNoSetting()
        {
            // Arrange
            SetEnvironmentVar("AWS_LAMBDA_FUNCTION_NAME", "myFunc");

            var xml =
                "<configuration xmlns=\"urn:newrelic-config\"  >" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "</configuration>";


            var configuration = CreateBootstrapConfiguration(xml);

            // Act
            var result = configuration.ServerlessModeEnabled;

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void ServerlessModeNotEnabled_When_ServerlessEnvVarNotSet_LambdaFuncEnvVarNotSet_ConfigHasNoSetting()
        {
            // Arrange
            var xml =
                "<configuration xmlns=\"urn:newrelic-config\"  >" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "</configuration>";


            var configuration = CreateBootstrapConfiguration(xml);

            // Act
            var result = configuration.ServerlessModeEnabled;

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void ServerlessModeNotEnabled_When_ServerlessEnvVarSetToFalse_ConfigHasNoSetting()
        {
            // Arrange
            SetEnvironmentVar("NEW_RELIC_SERVERLESS_MODE_ENABLED", "false");

            var xml =
                "<configuration xmlns=\"urn:newrelic-config\"  >" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "</configuration>";


            var configuration = CreateBootstrapConfiguration(xml);

            // Act
            var result = configuration.ServerlessModeEnabled;

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void ServerlessModeEnvVar_TakesPrecedenceOver_ConfigSetting()
        {
            // Arrange
            SetEnvironmentVar("NEW_RELIC_SERVERLESS_MODE_ENABLED", "false");
            var xml =
                "<configuration xmlns=\"urn:newrelic-config\" serverlessModeEnabled=\"true\"  >" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "</configuration>";


            var configuration = CreateBootstrapConfiguration(xml);

            // Act
            var result = configuration.ServerlessModeEnabled;

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void ServerlessModeEnvVar_TakesPrecedenceOver_LambdaFunctionEnvVar()
        {
            // Arrange
            SetEnvironmentVar("NEW_RELIC_SERVERLESS_MODE_ENABLED", "false");
            SetEnvironmentVar("AWS_LAMBDA_FUNCTION_NAME", "myFunc");

            var xml =
                "<configuration xmlns=\"urn:newrelic-config\" >" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "</configuration>";


            var configuration = CreateBootstrapConfiguration(xml);

            // Act
            var result = configuration.ServerlessModeEnabled;

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void ServerlessModeEnabled_WhenOnly_ConfigIsSet()
        {
            // Arrange

            var xml =
                "<configuration xmlns=\"urn:newrelic-config\" serverlessModeEnabled=\"true\" >" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "</configuration>";


            var configuration = CreateBootstrapConfiguration(xml);

            // Act
            var result = configuration.ServerlessModeEnabled;

            // Assert
            Assert.That(result, Is.False);
        }

        private BootstrapConfiguration CreateBootstrapConfiguration(string xml)
        {
            var configuration = new configuration();
            return new BootstrapConfiguration(configuration, "testfilename");
        }
    }
}
