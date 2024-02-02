// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NewRelic.Agent.Core.Tracer;
using NUnit.Framework;
using System;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest
{
    public class Class_AgentShim
    {
        [TestFixture, Category("JustMock"), Category("MockingProfiler")]
        public class Method_FinishTracer
        {
            private TestUtilities.Logging _logger;

            [OneTimeSetUp]
            public void TestFixtureSetUp()
            {
                var propInfo = typeof(AgentInitializer)
                    .GetProperty("InitializeAgent", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                propInfo.SetValue(null, new Action(() => { }));

                // Force early static initialization of AgentShim by interacting with it in some arbitrary way
                AgentInitializer.OnExit += (_, __) => { };
            }

            [SetUp]
            public void SetUp()
            {
                _logger = new TestUtilities.Logging();
            }

            [TearDown]
            public void TearDown()
            {
                _logger.Dispose();
            }

            [Test]
            public void returns_null_when_tracer_object_is_null()
            {
                // ARRANGE
                object tracer = null;

                // ACT
                AgentShim.FinishTracer(tracer, null, null);

                // ASSERT
                Assert.That(_logger.MessageCount, Is.EqualTo(0), "Expected no log entries but got: " + _logger.ToString());
            }

            [Test]
            public void does_not_throw_when_tracer_object_is_not_an_ITracer()
            {
                object tracer = new object();
                Assert.DoesNotThrow(() => AgentShim.FinishTracer(tracer, null, null));
            }

            [Test]
            public void returns_without_calling_finish_when_exception_object_is_not_an_exception()
            {
                // ARRANGE
                var tracer = Mock.Create<ITracer>(Behavior.Strict);
                Mock.Arrange(() => tracer.Finish(Arg.AnyObject, Arg.IsAny<Exception>())).OccursNever();
                var exception = new object();

                // ACT
                AgentShim.FinishTracer(tracer, null, exception);

                // ASSERT
                Mock.Assert(tracer);
            }

            [Test]
            public void calls_Finish_on_tracer_with_null_return_and_exception()
            {
                // ARRANGE
                var tracer = Mock.Create<ITracer>(Behavior.Strict);
                var retrn = null as object;
                var exception = null as Exception;
                Mock.Arrange(() => tracer.Finish(retrn, exception)).OccursOnce();

                // ACT
                AgentShim.FinishTracer(tracer, retrn, exception);

                // ASSERT
                Mock.Assert(tracer);
            }

            [Test]
            public void calls_Finish_on_tracer_with_return_passed_through()
            {
                // ARRANGE
                var tracer = Mock.Create<ITracer>(Behavior.Strict);
                var retrn = new object();
                var exception = null as Exception;
                Mock.Arrange(() => tracer.Finish(retrn, exception)).OccursOnce();

                // ACT
                AgentShim.FinishTracer(tracer, retrn, exception);

                // ASSERT
                Mock.Assert(tracer);
            }

            [Test]
            public void calls_Finish_on_tracer_with_exception_passed_through()
            {
                // ARRANGE
                var tracer = Mock.Create<ITracer>(Behavior.Strict);
                var retrn = null as object;
                var exception = new Exception();
                Mock.Arrange(() => tracer.Finish(retrn, exception)).OccursOnce();

                // ACT
                AgentShim.FinishTracer(tracer, retrn, exception);

                // ASSERT
                Mock.Assert(tracer);
            }

            [Test]
            public void Exception_does_not_bubble_up_when_thrown_from_FinishTracerImpl()
            {
                var tracer = Mock.Create<ITracer>(Behavior.Strict);
                Mock.Arrange(() => tracer.Finish(null as object, null as Exception)).Throws(new Exception());
                Assert.DoesNotThrow(() => AgentShim.FinishTracer(tracer, null, null));
            }
        }
    }
}
