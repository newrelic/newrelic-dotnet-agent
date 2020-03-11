using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	public interface ITransactionEventMaker
	{
		TransactionEventWireModel GetTransactionEvent(ImmutableTransaction immutableTransaction, AttributeCollection attributes);
	}

	public class TransactionEventMaker : ITransactionEventMaker
	{
		private readonly IAttributeService _attributeService;

		public TransactionEventMaker(IAttributeService attributeService)
		{
			_attributeService = attributeService;
		}

		public TransactionEventWireModel GetTransactionEvent(ImmutableTransaction immutableTransaction, AttributeCollection attributes)
		{
			var filteredAttributes = _attributeService.FilterAttributes(attributes, AttributeDestinations.TransactionEvent);
			var agentAttributes = filteredAttributes.GetAgentAttributesDictionary();
			var intrinsicAttributes = filteredAttributes.GetIntrinsicsDictionary();
			var userAttributes = filteredAttributes.GetUserAttributesDictionary();

			var transactionMetadata = immutableTransaction.TransactionMetadata;
			var isSynthetics = transactionMetadata.IsSynthetics;
			var priority = immutableTransaction.Priority;

			return new TransactionEventWireModel(userAttributes, agentAttributes, intrinsicAttributes, isSynthetics, priority);
		}
	}
}
