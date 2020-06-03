using NewRelic.Agent.Core.Attributes;

namespace NewRelic.Agent.Core.WireModels
{
    public class TransactionEventWireModel : EventWireModel
    {
        public TransactionEventWireModel(IAttributeValueCollection attribValues, bool isSynthetics, float priority)
            : base(AttributeDestinations.TransactionEvent, attribValues, isSynthetics, priority)
        {
        }
    }
}
