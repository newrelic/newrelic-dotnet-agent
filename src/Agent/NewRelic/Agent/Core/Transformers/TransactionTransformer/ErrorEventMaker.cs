using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
    public interface IErrorEventMaker
    {
        ErrorEventWireModel GetErrorEvent(ImmutableTransaction immutableTransaction, IAttributeValueCollection attribValues);
        ErrorEventWireModel GetErrorEvent(ErrorData errorData, IAttributeValueCollection attribValues, float priority);
    }

    public class ErrorEventMaker : IErrorEventMaker
    {
        private readonly IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc.AttributeDefs;

        public ErrorEventMaker(IAttributeDefinitionService attributeService)
        {
            _attribDefSvc = attributeService;
        }

        public ErrorEventWireModel GetErrorEvent(ErrorData errorData, IAttributeValueCollection attribValues, float priority)
        {
            // These attributes are for an ErrorEvent outside of a transaction

            _attribDefs.GetTypeAttribute(TypeAttributeValue.TransactionError).TrySetDefault(attribValues);
            _attribDefs.ErrorClass.TrySetValue(attribValues, errorData.ErrorTypeName);
            _attribDefs.ErrorDotMessage.TrySetValue(attribValues, errorData.ErrorMessage);
            _attribDefs.TimestampForError.TrySetValue(attribValues, errorData.NoticedAt);

            return new ErrorEventWireModel(attribValues, false, priority);
        }

        public ErrorEventWireModel GetErrorEvent(ImmutableTransaction immutableTransaction, IAttributeValueCollection attribValues)
        {
            var transactionMetadata = immutableTransaction.TransactionMetadata;
            var isSynthetics = transactionMetadata.IsSynthetics;
            var priority = immutableTransaction.Priority;

            _attribDefs.GetTypeAttribute(TypeAttributeValue.TransactionError).TrySetDefault(attribValues);
            return new ErrorEventWireModel(attribValues, isSynthetics, priority);
        }
    }
}
