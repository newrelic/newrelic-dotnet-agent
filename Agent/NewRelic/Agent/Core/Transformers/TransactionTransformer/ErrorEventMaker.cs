using JetBrains.Annotations;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;
using Attribute = NewRelic.Agent.Core.Transactions.Attribute;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	public interface IErrorEventMaker
	{
		[NotNull]
		ErrorEventWireModel GetErrorEvent([NotNull] ErrorData errorData, [NotNull] ImmutableTransaction immutableTransaction,
			[NotNull] Attributes transactionAttributes);

		[NotNull]
		ErrorEventWireModel GetErrorEvent([NotNull] ErrorData errorData, [NotNull] Attributes customAttributes);
	}

	public class ErrorEventMaker : IErrorEventMaker
	{
		[NotNull] private readonly IAttributeService _attributeService;

		public ErrorEventMaker([NotNull] IAttributeService attributeService)
		{
			_attributeService = attributeService;
		}

		public ErrorEventWireModel GetErrorEvent(ErrorData errorData, ImmutableTransaction immutableTransaction, Attributes transactionAttributes)
		{
			var filteredAttributes = _attributeService.FilterAttributes(transactionAttributes, AttributeDestinations.ErrorEvent);

			var typeAttribute = Attribute.BuildTypeAttribute(TypeAttributeValue.TransactionError);
			filteredAttributes.Add(typeAttribute);

			var errorClassAttribute = Attribute.BuildErrorClassAttribute(errorData.ErrorTypeName);
			filteredAttributes.Add(errorClassAttribute);

			var errorMessageAttribute = Attribute.BuildErrorDotMessageAttribute(errorData.ErrorMessage);
			filteredAttributes.Add(errorMessageAttribute);

			return CreateErrorEvent(immutableTransaction, filteredAttributes);
		}

		public ErrorEventWireModel GetErrorEvent([NotNull] ErrorData errorData, [NotNull] Attributes customAttributes)
		{
			// These attributes are for an ErrorEvent outside of a transaction

			var typeAttribute = Attribute.BuildTypeAttribute(TypeAttributeValue.TransactionError);
			customAttributes.Add(typeAttribute);

			var errorClassAttribute = Attribute.BuildErrorClassAttribute(errorData.ErrorTypeName);
			customAttributes.Add(errorClassAttribute);

			var errorMessageAttribute = Attribute.BuildErrorDotMessageAttribute(errorData.ErrorMessage);
			customAttributes.Add(errorMessageAttribute);

			var timestampAttribute = Attribute.BuildTimeStampAttribute(errorData.NoticedAt);
			customAttributes.Add(timestampAttribute);

			return CreateErrorEvent(customAttributes);
		}

		#region Private Helpers

		[NotNull]
		private ErrorEventWireModel CreateErrorEvent([NotNull] ImmutableTransaction immutableTransaction, [NotNull] Attributes filteredAttributes)
		{
			var agentAttributes = filteredAttributes.GetAgentAttributesDictionary();
			var intrinsicAttributes = filteredAttributes.GetIntrinsicsDictionary();
			var userAttributes = filteredAttributes.GetUserAttributesDictionary();

			var isSynthetics = immutableTransaction.TransactionMetadata.IsSynthetics;

			return new ErrorEventWireModel(agentAttributes, intrinsicAttributes, userAttributes, isSynthetics);
		}

		private ErrorEventWireModel CreateErrorEvent([NotNull] Attributes customAttributes)
		{
			var filteredAttributes = _attributeService.FilterAttributes(customAttributes, AttributeDestinations.ErrorEvent);
			var agentAttributes = filteredAttributes.GetAgentAttributesDictionary();
			var intrinsicAttributes = filteredAttributes.GetIntrinsicsDictionary();
			var userAttributes = filteredAttributes.GetUserAttributesDictionary();

			return new ErrorEventWireModel(agentAttributes, intrinsicAttributes, userAttributes, false);
		}


		#endregion Private Helpers
	}
}
