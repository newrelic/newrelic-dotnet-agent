using Telerik.JustMock;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.Segments.Tests
{
	public static class TransactionSegmentStateHelpers
	{
		public static ITransactionSegmentState GetItransactionSegmentState()
		{
			var transactionSegmentState = Mock.Create<ITransactionSegmentState>();

			var attributeService = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
			Mock.Arrange(() => transactionSegmentState.AttribDefs).Returns(attributeService.AttributeDefs);

			var configurationService = Mock.Create<IConfigurationService>();

			var errorService = new Errors.ErrorService(configurationService);
			Mock.Arrange(() => transactionSegmentState.ErrorService).Returns(errorService);

			return transactionSegmentState;
		}
	}
}
