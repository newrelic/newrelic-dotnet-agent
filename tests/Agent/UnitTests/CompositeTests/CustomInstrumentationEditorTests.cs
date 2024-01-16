// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Commands;
using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;
using Telerik.JustMock;

namespace CompositeTests
{
    [TestFixture]
    public class CustomInstrumentationEditorTests
    {
        private static CompositeTestAgent _compositeTestAgent;

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent();

            _compositeTestAgent.ServerConfiguration.Instrumentation = new List<ServerConfiguration.InstrumentationConfig>
            {
                new ServerConfiguration.InstrumentationConfig
                {
                    Name = "live_instrumentation",
                    Config = "SomeXml"
                }
            };
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

        [Test]
        public void CustomInstrumentationEditor_WhenLiveInstrumentationEditorIsClearedThenLiveInstrumentationGetsSetToEmpty()
        {
            _compositeTestAgent.LocalConfiguration.customInstrumentationEditor.enabled = true;
            _compositeTestAgent.PushConfiguration();

            Mock.Assert(() => _compositeTestAgent.NativeMethods
                .AddCustomInstrumentation(Arg.Matches<string>(x => x == "live_instrumentation"), Arg.Matches<string>(x => x == "SomeXml")), Occurs.Once());
            Mock.Assert(() => _compositeTestAgent.NativeMethods.ApplyCustomInstrumentation(), Occurs.Once());

            ProcessEmptyLiveInstrumentation();

            Mock.Assert(() => _compositeTestAgent.NativeMethods
                .AddCustomInstrumentation(Arg.Matches<string>(x => x == "live_instrumentation"), Arg.Matches<string>(x => x == string.Empty)), Occurs.Once());
            Mock.Assert(() => _compositeTestAgent.NativeMethods.ApplyCustomInstrumentation(), Occurs.Exactly(2));
        }

        private void ProcessEmptyLiveInstrumentation()
        {
            var instrumentationUpdateCommand = new InstrumentationUpdateCommand(_compositeTestAgent.InstrumentationService);
            var serializedUpdateCommandWithBlankLiveInstrumentation = "[123,{\"name\":\"instrumentation_update\",\"arguments\":{\"instrumentation\":{\"config\":\"\"}}}]";
            var commandModel = JsonConvert.DeserializeObject<CommandModel>(serializedUpdateCommandWithBlankLiveInstrumentation, new JsonArrayConverter());
            instrumentationUpdateCommand.Process(commandModel.Details.Arguments);
        }
    }
}
