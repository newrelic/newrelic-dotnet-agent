using JetBrains.Annotations;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	public interface ITransactionEventMaker
	{
		[NotNull]
		TransactionEventWireModel GetTransactionEvent([NotNull] ImmutableTransaction immutableTransaction, [NotNull] Attributes attributes);
	}

	public class TransactionEventMaker : ITransactionEventMaker
	{
		[NotNull]
		private readonly IAttributeService _attributeService;

		public TransactionEventMaker([NotNull] IAttributeService attributeService)
		{
			_attributeService = attributeService;
		}

		public TransactionEventWireModel GetTransactionEvent(ImmutableTransaction immutableTransaction, Attributes attributes)
		{
			var filteredAttributes = _attributeService.FilterAttributes(attributes, AttributeDestinations.TransactionEvent);
			var agentAttributes = filteredAttributes.GetAgentAttributesDictionary();
			var intrinsicAttributes = filteredAttributes.GetIntrinsicsDictionary();
			var userAttributes = filteredAttributes.GetUserAttributesDictionary();

			var isSynthetics = immutableTransaction.TransactionMetadata.IsSynthetics;

			return new TransactionEventWireModel(userAttributes, agentAttributes, intrinsicAttributes, isSynthetics);
		}
	}
}
