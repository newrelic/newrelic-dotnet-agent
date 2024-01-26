// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Testing.Assertions;
using Newtonsoft.Json;
using NUnit.Framework;

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

            Assert.That(commandModel, Is.Not.Null);
            Assert.That(commandModel.Details, Is.Not.Null);
            NrAssert.Multiple
                (
                () => Assert.That(commandModel.CommandId, Is.EqualTo(1)),
                () => Assert.That(commandModel.Details.Name, Is.EqualTo("some name")),
                () => Assert.That(commandModel.Details.Arguments, Has.Count.EqualTo(1)),
                () => Assert.That(commandModel.Details.Arguments["arg"], Is.EqualTo("value"))
                );
        }
    }
}
