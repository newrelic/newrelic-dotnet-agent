using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	public interface IErrorEventMaker
	{
		ErrorEventWireModel GetErrorEvent(ImmutableTransaction immutableTransaction, AttributeCollection transactionAttributes);
		ErrorEventWireModel GetErrorEvent(ErrorData errorData, AttributeCollection customAttributes, float priority);
	}

	public class ErrorEventMaker : IErrorEventMaker
	{
		private readonly IAttributeService _attributeService;

		public ErrorEventMaker(IAttributeService attributeService)
		{
			_attributeService = attributeService;
		}

		public ErrorEventWireModel GetErrorEvent(ImmutableTransaction immutableTransaction, AttributeCollection transactionAttributes)
		{
			var filteredAttributes = _attributeService.FilterAttributes(transactionAttributes, AttributeDestinations.ErrorEvent);

			return CreateErrorEvent(immutableTransaction, filteredAttributes);
		}

		public ErrorEventWireModel GetErrorEvent(ErrorData errorData, AttributeCollection customAttributes, float priority)
		{
			// These attributes are for an ErrorEvent outside of a transaction

			// WARNING: It is important that filtering happens on attributes to prevent leaking sensitive data. 
			// Currently, custom attributes are filtered before this method and everything is refiltered in CreateErrorEvent

			var typeAttribute = Attribute.BuildTypeAttribute(TypeAttributeValue.TransactionError);
			customAttributes.Add(typeAttribute);

			var errorClassAttribute = Attribute.BuildErrorClassAttribute(errorData.ErrorTypeName);
			customAttributes.Add(errorClassAttribute);

			var errorMessageAttribute = Attribute.BuildErrorDotMessageAttribute(errorData.ErrorMessage);
			customAttributes.Add(errorMessageAttribute);

			var errorTimeStampAttribute = Attribute.BuildErrorTimeStampAttribute(errorData.NoticedAt);
			customAttributes.Add(errorTimeStampAttribute);

			return CreateErrorEvent(customAttributes, priority);
		}

		#region Private Helpers

		private ErrorEventWireModel CreateErrorEvent(ImmutableTransaction immutableTransaction, AttributeCollection filteredAttributes)
		{
			var agentAttributes = filteredAttributes.GetAgentAttributesDictionary();
			var intrinsicAttributes = filteredAttributes.GetIntrinsicsDictionary();
			var userAttributes = filteredAttributes.GetUserAttributesDictionary();

			var transactionMetadata = immutableTransaction.TransactionMetadata;
			var isSynthetics = transactionMetadata.IsSynthetics;
			var priority = immutableTransaction.Priority;

			return new ErrorEventWireModel(agentAttributes, intrinsicAttributes, userAttributes, isSynthetics, priority);
		}

		private ErrorEventWireModel CreateErrorEvent(AttributeCollection customAttributes, float  priority)
		{
			var filteredAttributes = _attributeService.FilterAttributes(customAttributes, AttributeDestinations.ErrorEvent);
			var agentAttributes = filteredAttributes.GetAgentAttributesDictionary();
			var intrinsicAttributes = filteredAttributes.GetIntrinsicsDictionary();
			var userAttributes = filteredAttributes.GetUserAttributesDictionary();

			return new ErrorEventWireModel(agentAttributes, intrinsicAttributes, userAttributes, false, priority);
		}


		#endregion Private Helpers
	}
}
