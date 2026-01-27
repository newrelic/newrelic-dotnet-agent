// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer;

public interface ITransactionEventMaker
{
    TransactionEventWireModel GetTransactionEvent(ImmutableTransaction immutableTransaction, IAttributeValueCollection attributes);
}

public class TransactionEventMaker : ITransactionEventMaker
{
    private readonly IAttributeDefinitionService _attribDefSvc;
    private IAttributeDefinitions _attribDefs => _attribDefSvc?.AttributeDefs;

    public TransactionEventMaker(IAttributeDefinitionService attribDefSvc)
    {
        _attribDefSvc = attribDefSvc;
    }

    public TransactionEventWireModel GetTransactionEvent(ImmutableTransaction immutableTransaction, IAttributeValueCollection attribValues)
    {
        var transactionMetadata = immutableTransaction.TransactionMetadata;
        var isSynthetics = transactionMetadata.IsSynthetics;
        var priority = immutableTransaction.Priority;

        _attribDefs.GetTypeAttribute(TypeAttributeValue.Transaction).TrySetDefault(attribValues);

        return new TransactionEventWireModel(attribValues, isSynthetics, priority);
    }
}