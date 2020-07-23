using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core
{

    [TestFixture]
    public class BaseAgentTest
    {
        protected IAgent agent;

        [SetUp]
        public void SetUp()
        {
            agent = Mock.Create<IAgent>();
        }
    }
}
