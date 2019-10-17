using System.Reflection;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Tracer;
using NUnit.Framework;
using System;
using Telerik.JustMock;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace NewRelic.Agent.Core.UnitTest
{
	public class Class_AgentShim
	{
		[TestFixture, Category("JustMock"), Category("MockingProfiler"), Ignore("This fixture spins up an agent which has side effects that cause other tests to fail.  A mocking profiler is the only way around this, and we would like to get away from using a mocking profiler.  The code under test needs a refactor at some point, but that point isn't today.")]
		public class Method_GetTracer
		{
			[NotNull]
			private IAgentManager _agentManager;

			[OneTimeSetUp]
			public void TestFixtureSetUp()
			{
				// Force early static initialization of AgentShim by interacting with it in some arbitrary way
				AgentInitializer.OnExit += (_, __) => { };
			}

			[SetUp]
			public void SetUp()
			{
				_agentManager = Mock.Create<IAgentManager>(Behavior.Strict);
				SetAgentInstance(_agentManager);
			}

			[TearDown]
			public void TearDown()
			{
				SetAgentInstance(null);
			}

			[Test]
			public void returns_null_when_agent_is_null()
			{
				// ARRANGE
				SetAgentInstance(null);

				// ACT
				var result = AgentShim.GetTracer(null, 0, null, null, null, null, null, null, null, null, 0);

				// ASSERT
				Assert.IsNull(result);
			}

			[Test]
			public void returns_null_when_agent_state_is_starting()
			{
				// ARRANGE
				Mock.Arrange(() => _agentManager.State).Returns(AgentState.Starting);

				// ACT
				var result = AgentShim.GetTracer(null, 0, null, null, null, null, null, null, null, null, 0);

				// ASSERT
				Assert.IsNull(result);
			}

			[Test]
			public void returns_null_when_agent_state_is_uninitialized()
			{
				// ARRANGE
				Mock.Arrange(() => _agentManager.State).Returns(AgentState.Uninitialized);

				// ACT
				var result = AgentShim.GetTracer(null, 0, null, null, null, null, null, null, null, null, 0);

				// ASSERT
				Assert.IsNull(result);
			}

			[Test]
			public void returns_null_when_agent_state_is_stopped()
			{
				// ARRANGE
				Mock.Arrange(() => _agentManager.State).Returns(AgentState.Stopped);

				// ACT
				var result = AgentShim.GetTracer(null, 0, null, null, null, null, null, null, null, null, 0);

				// ASSERT
				Assert.IsNull(result);
			}

			[Test]
			public void returns_null_when_agent_state_is_stopping()
			{
				// ARRANGE
				Mock.Arrange(() => _agentManager.State).Returns(AgentState.Stopping);

				// ACT
				var result = AgentShim.GetTracer(null, 0, null, null, null, null, null, null, null, null, 0);

				// ASSERT
				Assert.IsNull(result);
			}

			[Test]
			public void returns_result_of_Agent_Instance_GetTracerImpl_when_agent_state_is_started()
			{
				// ARRANGE
				var tracer = Mock.Create<ITracer>(Behavior.Strict);
				Mock.Arrange(() => _agentManager.State).Returns(AgentState.Started);
				Mock.Arrange(() => _agentManager.GetTracerImpl(null, 0, null, null, null, null, null, null, null, null, 0)).IgnoreArguments().Returns(tracer);

				// ACT
				var result = AgentShim.GetTracer(null, 0, null, null, null, null, null, null, null, null, 0);

				// ASSERT
				Assert.AreEqual(tracer, result);
			}
		}

		[TestFixture, Category("JustMock"), Category("MockingProfiler")]
		public class Method_FinishTracer
		{
			[NotNull]
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
				Object tracer = null;

				// ACT
				AgentShim.FinishTracer(tracer, null, null);

				// ASSERT
				Assert.AreEqual(0, _logger.MessageCount);
			}

			[Test]
			public void does_not_throw_when_tracer_object_is_not_an_ITracer()
			{
				Object tracer = new Object();
				Assert.DoesNotThrow(() => AgentShim.FinishTracer(tracer, null, null));
			}

			[Test]
			public void returns_without_calling_finish_when_exception_object_is_not_an_exception()
			{
				// ARRANGE
				var tracer = Mock.Create<ITracer>(Behavior.Strict);
				Mock.Arrange(() => tracer.Finish(Arg.AnyObject, Arg.IsAny<Exception>())).OccursNever();
				var exception = new Object();

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
				var retrn = null as Object;
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
				var retrn = new Object();
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
				var retrn = null as Object;
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
				Mock.Arrange(() => tracer.Finish(null as Object, null as Exception)).Throws(new Exception());
				Assert.DoesNotThrow(() => AgentShim.FinishTracer(tracer, null, null));
			}
		}

		private static void SetAgentInstance(IAgentManager agentManager)
		{
			var agentSingleton = typeof(AgentManager).GetField("singleton", BindingFlags.NonPublic | BindingFlags.Static).GetValue(agentManager) as Singleton<IAgentManager>;
			agentSingleton.SetInstance(agentManager);
		}

	}
}
