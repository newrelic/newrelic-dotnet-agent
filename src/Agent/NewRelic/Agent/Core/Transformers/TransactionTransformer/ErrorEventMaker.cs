using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;
using Attribute = NewRelic.Agent.Core.Transactions.Attribute;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
    public interface IErrorEventMaker
    {
        ErrorEventWireModel GetErrorEvent(ErrorData errorData, ImmutableTransaction immutableTransaction,
            Attributes transactionAttributes);
        ErrorEventWireModel GetErrorEvent(ErrorData errorData, Attributes customAttributes);
    }

    public class ErrorEventMaker : IErrorEventMaker
    {
        private readonly IAttributeService _attributeService;

        public ErrorEventMaker(IAttributeService attributeService)
        {
            _attributeService = attributeService;
        }

        public ErrorEventWireModel GetErrorEvent(ErrorData errorData, ImmutableTransaction immutableTransaction, Attributes transactionAttributes)
        {
            var filteredAttributes = _attributeService.FilterAttributes(transactionAttributes, AttributeDestinations.ErrorEvent);


            // *** DO NOT COPY THIS PATTERN ***
            // Because of the cached filtering model, this has to be added here to avoid being filtered out due to 
            // the identically named attribute for Transaction Events.
            //
            // This style of attribute adding can result in security issues if not done correctly to filter out
            // based on HSM logic, etc.
            //
            // Most attributes should be added via the TransactionAttributeMaker.
            // The attribute system needs a large overhaul to help prevent mistakes and also prevent collection of
            // sensitive data that we will just drop on the floor.
            var typeAttribute = Attribute.BuildTypeAttribute(TypeAttributeValue.TransactionError);
            filteredAttributes.Add(typeAttribute);

            return CreateErrorEvent(immutableTransaction, filteredAttributes);
        }

        public ErrorEventWireModel GetErrorEvent(ErrorData errorData, Attributes customAttributes)
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

            var timestampAttribute = Attribute.BuildTimeStampAttribute(errorData.NoticedAt);
            customAttributes.Add(timestampAttribute);


            return CreateErrorEvent(customAttributes);
        }

        #region Private Helpers
        private ErrorEventWireModel CreateErrorEvent(ImmutableTransaction immutableTransaction, Attributes filteredAttributes)
        {
            var agentAttributes = filteredAttributes.GetAgentAttributesDictionary();
            var intrinsicAttributes = filteredAttributes.GetIntrinsicsDictionary();
            var userAttributes = filteredAttributes.GetUserAttributesDictionary();

            var isSynthetics = immutableTransaction.TransactionMetadata.IsSynthetics;

            return new ErrorEventWireModel(agentAttributes, intrinsicAttributes, userAttributes, isSynthetics);
        }

        private ErrorEventWireModel CreateErrorEvent(Attributes customAttributes)
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
