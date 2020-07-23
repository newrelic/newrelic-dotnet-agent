using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared.Models;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NewRelic.Agent.IntegrationTests
{
	public class DataTransmissionDefaults : IClassFixture<MvcWithCollectorFixture>
	{
		[NotNull]
		private readonly MvcWithCollectorFixture _fixture;

		private IEnumerable<CollectedRequest> _collectedRequests = null;

		public DataTransmissionDefaults([NotNull] MvcWithCollectorFixture fixture)
		{
			_fixture = fixture;

			_fixture.AddActions(
				setupConfiguration: () =>
				{
					var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
				},
				exerciseApplication: () =>
				{
					_fixture.Get();
					_collectedRequests = _fixture.GetCollectedRequests();
				}
			);
			_fixture.Initialize();
		}

		[Fact]
		public void Test()
		{
			Assert.NotNull(_collectedRequests);
			var request = _collectedRequests.FirstOrDefault(x => x.Querystring.FirstOrDefault(y => y.Key == "method").Value == "connect");
			Assert.NotNull(request);
			Assert.True(request.Method == "POST");
			Assert.True(request.ContentEncoding.First() == "deflate");
			var decompressedBody = Decompressor.DeflateDecompress(request.RequestBody);
			Assert.NotEmpty(decompressedBody);
		}
	}
}
