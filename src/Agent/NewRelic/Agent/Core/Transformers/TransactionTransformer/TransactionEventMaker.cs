/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
    public interface ITransactionEventMaker
    {
        TransactionEventWireModel GetTransactionEvent(ImmutableTransaction immutableTransaction, Attributes attributes);
    }

    public class TransactionEventMaker : ITransactionEventMaker
    {
        private readonly IAttributeService _attributeService;

        public TransactionEventMaker(IAttributeService attributeService)
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
