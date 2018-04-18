using NewRelic.Agent.Core.Configuration;
using JetBrains.Annotations;
using NUnit.Framework;
using Telerik.JustMock;

namespace CompositeTests
{
	[TestFixture]
	public class CustomInstrumentationEditorTests
	{
		[NotNull]
		private static CompositeTestAgent _compositeTestAgent;

		[SetUp]
		public void SetUp()
		{
			_compositeTestAgent = new CompositeTestAgent();

			ServerConfiguration.InstrumentationConfig[] instrumentationConfig = { new ServerConfiguration.InstrumentationConfig() };
			_compositeTestAgent.ServerConfiguration.Instrumentation = instrumentationConfig;
		}

		[TearDown]
		public static void TearDown()
		{
			_compositeTestAgent.Dispose();
		}

		[Test]
		public void CustomInstrumentationEditor_Applied()
		{
			_compositeTestAgent.PushConfiguration();
			Mock.Assert(() => _compositeTestAgent.NativeMethods.ApplyCustomInstrumentation(), Occurs.Once());
		}

		[Test]
		public void CustomInstrumentationEditor_HighSecurity_NotApplied()
		{
			_compositeTestAgent.ServerConfiguration.HighSecurityEnabled = true;
			_compositeTestAgent.LocalConfiguration.highSecurity.enabled = true;
			_compositeTestAgent.PushConfiguration();

			Mock.Assert(() => _compositeTestAgent.NativeMethods.ApplyCustomInstrumentation(), Occurs.Never());
		}

		[Test]
		public void CustomInstrumentationEditor_Disabled_NotApplied()
		{
			_compositeTestAgent.LocalConfiguration.customInstrumentationEditor.enabled = false;
			_compositeTestAgent.PushConfiguration();

			Mock.Assert(() => _compositeTestAgent.NativeMethods.ApplyCustomInstrumentation(), Occurs.Never());
		}
	}
}
