// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Web;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NUnit.Framework;
using Telerik.JustMock;
using HttpException = NewRelic.Agent.Core.DataTransport.HttpException;

namespace NewRelic.Agent.Core.DataTransport
{
    [TestFixture]
    public class ConnectionManagerTests
    {
        private DisposableCollection _disposableCollection;
        private IConfiguration _configuration;
        private IConnectionHandler _connectionHandler;
        private IScheduler _scheduler;

        [SetUp]
        public void SetUp()
        {
            _disposableCollection = new DisposableCollection();

            _configuration = Mock.Create<IConfiguration>();
            _disposableCollection.Add(new ConfigurationAutoResponder(_configuration));

            _connectionHandler = Mock.Create<IConnectionHandler>();
            _scheduler = Mock.Create<IScheduler>();
        }

        [TearDown]
        public void TearDown()
        {
            _disposableCollection.Dispose();
        }

        [Test]
        public void AttemptAutoStart_CallsConnectSynchronously_IfAutoStartAndSyncStartupIsOn()
        {
            Mock.Arrange(() => _configuration.CollectorSyncStartup).Returns(true);
            Mock.Arrange(() => _configuration.AutoStartAgent).Returns(true);

            // Act
            using (var connectionManager = new ConnectionManager(_connectionHandler, _scheduler))
            {
                connectionManager.AttemptAutoStart();
                Mock.Assert(() => _connectionHandler.Connect());
            }
        }

        [Test]
        public void AttemptAutoStart_SchedulesConnectAsynchronously_IfAutoStartIsOnAndSyncStartupIsOff()
        {
            Mock.Arrange(() => _configuration.CollectorSyncStartup).Returns(false);
            Mock.Arrange(() => _configuration.AutoStartAgent).Returns(true);

            Action scheduledAction = null;
            Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
                .DoInstead<Action, TimeSpan>((action, timespan) => scheduledAction = action);

            // Act
            using (var connectionManager = new ConnectionManager(_connectionHandler, _scheduler))
            {
                connectionManager.AttemptAutoStart();

                // Connect shouldn't occur until scheduled action is invoked
                Mock.Assert(() => _connectionHandler.Connect(), Occurs.Never());

                scheduledAction();
                Mock.Assert(() => _connectionHandler.Connect());
            }
        }


        [Test]
        [TestCase("ForceRestartException")]
        [TestCase("HttpException")]
        [TestCase("HttpRequestException")]
        [TestCase("SocketException")]
        [TestCase("IOException")]
        [TestCase("OperationCanceledException")]
        public void AttemptAutoStart_SchedulesReconnect_IfCertainExceptionOccurs(string execeptionType)
        {
            Exception ex = null;
            switch (execeptionType)
            {
                case "ForceRestartException":
                    ex = new HttpException(HttpStatusCode.Conflict, null);
                    break;
                case "HttpException":
                    ex = new HttpException(HttpStatusCode.MethodNotAllowed, null);
                    break;
                case "HttpRequestException":
                    ex = new HttpRequestException();
                    break;
                case "SocketException":
                    ex = new SocketException();
                    break;
                case "IOException":
                    ex = new IOException();
                    break;
                case "OperationCanceledException":
                    ex = new OperationCanceledException();
                    break;
            }


            Mock.Arrange(() => _configuration.CollectorSyncStartup).Returns(true);
            Mock.Arrange(() => _configuration.AutoStartAgent).Returns(true);

            Mock.Arrange(() => _connectionHandler.Connect())
                .Throws(ex);

            Action scheduledAction = null;
            Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
                .DoInstead<Action, TimeSpan>((action, timespan) => scheduledAction = action);

            // Act
            using (var connectionManager = new ConnectionManager(_connectionHandler, _scheduler))
            {
                connectionManager.AttemptAutoStart();

                Mock.Assert(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()));

                scheduledAction();
                Mock.Assert(() => _connectionHandler.Connect(), Occurs.Exactly(2));
            }
        }

        [TestCaseSource(typeof(ConnectionManagerTests), nameof(ShutdownScenarios))]
        public void AttemptAutoStart_PublishesShutdownAgentEvent_IfCertainExceptionsOccur(Exception testData)
        {
            Mock.Arrange(() => _configuration.CollectorSyncStartup).Returns(true);
            Mock.Arrange(() => _configuration.AutoStartAgent).Returns(true);

            Mock.Arrange(() => _connectionHandler.Connect())
                .Throws(testData);

            // Act
            using (new EventExpectation<KillAgentEvent>())
            using (var connectionManager = new ConnectionManager(_connectionHandler, _scheduler))
            {
                connectionManager.AttemptAutoStart();
            }
        }

        private static TestCaseData[] ShutdownScenarios
        {
            get
            {
                var testCases = new[] {
                    new TestCaseData(new Exception()),
                    new TestCaseData(new HttpException(HttpStatusCode.Gone, null))
                };

                return testCases;
            }
        }

        [Test]
        public void AttemptAutoStart_DoublesReconnectTimeForEachReconnect_UntilHittingFiveMinutes()
        {
            Mock.Arrange(() => _configuration.CollectorSyncStartup).Returns(true);
            Mock.Arrange(() => _configuration.AutoStartAgent).Returns(true);

            Mock.Arrange(() => _connectionHandler.Connect())
                .Throws(new HttpException(HttpStatusCode.InternalServerError, null));

            Action scheduledAction = null;
            var scheduledTime = new TimeSpan();
            Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
                .DoInstead<Action, TimeSpan>((action, time) =>
                {
                    scheduledAction = action;
                    scheduledTime = time;
                });

            // Act
            using (var connectionManager = new ConnectionManager(_connectionHandler, _scheduler))
            {
                connectionManager.AttemptAutoStart();

                Mock.Assert(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()));
                Assert.AreEqual(15, scheduledTime.TotalSeconds);

                scheduledAction();
                Assert.AreEqual(15, scheduledTime.TotalSeconds);

                scheduledAction();
                Assert.AreEqual(30, scheduledTime.TotalSeconds);

                scheduledAction();
                Assert.AreEqual(60, scheduledTime.TotalSeconds);

                scheduledAction();
                Assert.AreEqual(120, scheduledTime.TotalSeconds);

                scheduledAction();
                Assert.AreEqual(300, scheduledTime.TotalSeconds);

                scheduledAction();
                Assert.AreEqual(300, scheduledTime.TotalSeconds);

                scheduledAction();
                Assert.AreEqual(300, scheduledTime.TotalSeconds);
            }
        }
    }
}
