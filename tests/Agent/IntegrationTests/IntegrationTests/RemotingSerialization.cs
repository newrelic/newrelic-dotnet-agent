using Xunit;

namespace NewRelic.Agent.IntegrationTests
{
    public class RemotingSerialization : IClassFixture<RemoteServiceFixtures.OwinRemotingFixture>
    {
        private readonly RemoteServiceFixtures.OwinRemotingFixture _fixture;

        string _tcpResponse;
        string _httpResponse;

        public RemotingSerialization(RemoteServiceFixtures.OwinRemotingFixture fixture)
        {
            _fixture = fixture;
            _fixture.AddActions(

                exerciseApplication: () =>
                {
                    _tcpResponse = _fixture.GetObjectTcp();
                    _httpResponse = _fixture.GetObjectHttp();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            Assert.True(_tcpResponse == "\"No exception\"");
            Assert.True(_httpResponse == "\"No exception\"");
        }
    }
}
