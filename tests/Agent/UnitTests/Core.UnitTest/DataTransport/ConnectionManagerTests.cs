using System;
using System.IO;
using System.Net.Sockets;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Exceptions;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NUnit.Framework;
using Telerik.JustMock;

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
        public void Constructor_CallsConnectSynchronously_IfAutoStartAndSyncStartupIsOn()
        {
            Mock.Arrange(() => _configuration.CollectorSyncStartup).Returns(true);
            Mock.Arrange(() => _configuration.AutoStartAgent).Returns(true);

            // Act (construct ConnectionManager)
            using (new ConnectionManager(_connectionHandler, _scheduler))
            {
                Mock.Assert(() => _connectionHandler.Connect());
            }
        }

        [Test]
        public void Constructor_SchedulesConnectAsynchronously_IfAutoStartIsOnAndSyncStartupIsOff()
        {
            Mock.Arrange(() => _configuration.CollectorSyncStartup).Returns(false);
            Mock.Arrange(() => _configuration.AutoStartAgent).Returns(true);

            Action scheduledAction = null;
            Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
                .DoInstead<Action, TimeSpan>((action, timespan) => scheduledAction = action);

            // Act (construct ConnectionManager)
            using (new ConnectionManager(_connectionHandler, _scheduler))
            {
                // Connect shouldn't occur until scheduled action is invoked
                Mock.Assert(() => _connectionHandler.Connect(), Occurs.Never());

                scheduledAction();
                Mock.Assert(() => _connectionHandler.Connect());
            }
        }


        [Test]
        [TestCase("ForceRestartException")]
        [TestCase("ConnectionException")]
        [TestCase("ServiceUnavailableException")]
        [TestCase("SocketException")]
        [TestCase("IOException")]
        public void Constructor_SchedulesReconnect_IfCertainExceptionOccurs(string execeptionType)
        {
            Exception ex = null;
            switch (execeptionType)
            {
                case "ForceRestartException":
                    ex = new ForceRestartException(null);
                    break;
                case "ConnectionException":
                    ex = new ConnectionException(null);
                    break;
                case "ServiceUnavailableException":
                    ex = new ServiceUnavailableException(null);
                    break;
                case "SocketException":
                    ex = new SocketException();
                    break;
                case "IOException":
                    ex = new IOException();
                    break;
            }


            Mock.Arrange(() => _configuration.CollectorSyncStartup).Returns(true);
            Mock.Arrange(() => _configuration.AutoStartAgent).Returns(true);

            Mock.Arrange(() => _connectionHandler.Connect())
                .Throws(ex);

            Action scheduledAction = null;
            Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
                .DoInstead<Action, TimeSpan>((action, timespan) => scheduledAction = action);

            // Act (construct ConnectionManager)
            using (new ConnectionManager(_connectionHandler, _scheduler))
            {
                Mock.Assert(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()));

                scheduledAction();
                Mock.Assert(() => _connectionHandler.Connect(), Occurs.Exactly(2));
            }
        }

        [Test]
        public void Constructor_PublishesShutdownAgentEvent_IfAnyOtherExceptionOccurs()
        {
            Mock.Arrange(() => _configuration.CollectorSyncStartup).Returns(true);
            Mock.Arrange(() => _configuration.AutoStartAgent).Returns(true);

            Mock.Arrange(() => _connectionHandler.Connect())
                .Throws(new Exception());

            // Act (construct ConnectionManager)
            using (new EventExpectation<KillAgentEvent>())
            using (new ConnectionManager(_connectionHandler, _scheduler))
            {
            }
        }

        [Test]
        public void Constructor_DoublesReconnectTimeForEachReconnect_UntilHittingFiveMinutes()
        {
            Mock.Arrange(() => _configuration.CollectorSyncStartup).Returns(true);
            Mock.Arrange(() => _configuration.AutoStartAgent).Returns(true);

            Mock.Arrange(() => _connectionHandler.Connect())
                .Throws(new ServiceUnavailableException(null));

            Action scheduledAction = null;
            var scheduledTime = new TimeSpan();
            Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
                .DoInstead<Action, TimeSpan>((action, time) =>
                {
                    scheduledAction = action;
                    scheduledTime = time;
                });

            // Act (construct ConnectionManager)
            using (new ConnectionManager(_connectionHandler, _scheduler))
            {
                Mock.Assert(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()));
                Assert.AreEqual(5, scheduledTime.TotalSeconds);

                scheduledAction();
                Assert.AreEqual(10, scheduledTime.TotalSeconds);

                scheduledAction();
                Assert.AreEqual(20, scheduledTime.TotalSeconds);

                scheduledAction();
                Assert.AreEqual(40, scheduledTime.TotalSeconds);

                scheduledAction();
                Assert.AreEqual(80, scheduledTime.TotalSeconds);

                scheduledAction();
                Assert.AreEqual(160, scheduledTime.TotalSeconds);

                scheduledAction();
                Assert.AreEqual(300, scheduledTime.TotalSeconds);

                scheduledAction();
                Assert.AreEqual(300, scheduledTime.TotalSeconds);
            }
        }
    }
}
