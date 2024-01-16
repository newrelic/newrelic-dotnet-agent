// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Commands
{
    [TestFixture]
    public class CommandModelTests
    {
        [Test]
        public void deserializes_correctly()
        {
            const string json = @"[1, {""name"": ""some name"", ""arguments"": {""arg"": ""value""}}]";

            var commandModel = JsonConvert.DeserializeObject<CommandModel>(json);

            ClassicAssert.NotNull(commandModel);
            ClassicAssert.NotNull(commandModel.Details);
            NrAssert.Multiple
                (
                () => ClassicAssert.AreEqual(1, commandModel.CommandId),
                () => ClassicAssert.AreEqual("some name", commandModel.Details.Name),
                () => ClassicAssert.AreEqual(1, commandModel.Details.Arguments.Count),
                () => ClassicAssert.AreEqual("value", commandModel.Details.Arguments["arg"])
                );
        }
    }
}
