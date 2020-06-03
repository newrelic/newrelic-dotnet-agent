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

            Assert.NotNull(commandModel);
            Assert.NotNull(commandModel.Details);
            NrAssert.Multiple
                (
                () => Assert.AreEqual(1, commandModel.CommandId),
                () => Assert.AreEqual("some name", commandModel.Details.Name),
                () => Assert.AreEqual(1, commandModel.Details.Arguments.Count),
                () => Assert.AreEqual("value", commandModel.Details.Arguments["arg"])
                );
        }
    }
}
