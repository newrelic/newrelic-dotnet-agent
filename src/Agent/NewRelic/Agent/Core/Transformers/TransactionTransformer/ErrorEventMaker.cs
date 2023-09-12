// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;
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
        private const string SetErrorGroupSupportabilityName = "ErrorEventMakerSetErrorGroup";
        private const string ExceptionAttributeName = "exception";
        private const string StackTraceAttributeName = "stack_trace";

        private readonly IConfigurationService _configurationService;
        private readonly IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc.AttributeDefs;
        private readonly IAgentTimerService _agentTimerService;

        public ErrorEventMaker(IAttributeDefinitionService attributeService, IConfigurationService configurationService, IAgentTimerService agentTimerService)
        {
            _attribDefSvc = attributeService;
            _configurationService = configurationService;
            _agentTimerService = agentTimerService;
        }

        public ErrorEventWireModel GetErrorEvent(ErrorData errorData, IAttributeValueCollection attribValues, float priority)
        {
            // These attributes are for an ErrorEvent outside of a transaction

            _attribDefs.GetTypeAttribute(TypeAttributeValue.TransactionError).TrySetDefault(attribValues);
            _attribDefs.ErrorClass.TrySetValue(attribValues, errorData.ErrorTypeName);
            _attribDefs.ErrorDotMessage.TrySetValue(attribValues, errorData.ErrorMessage);
            _attribDefs.TimestampForError.TrySetValue(attribValues, errorData.NoticedAt);

            SetErrorGroup(errorData, attribValues);
            return new ErrorEventWireModel(attribValues, false, priority);
        }

        public ErrorEventWireModel GetErrorEvent(ImmutableTransaction immutableTransaction, IAttributeValueCollection attribValues)
        {
            var transactionMetadata = immutableTransaction.TransactionMetadata;
            var isSynthetics = transactionMetadata.IsSynthetics;
            var priority = immutableTransaction.Priority;

            _attribDefs.GetTypeAttribute(TypeAttributeValue.TransactionError).TrySetDefault(attribValues);
            SetErrorGroup(immutableTransaction.TransactionMetadata?.ReadOnlyTransactionErrorState?.ErrorData, attribValues);
            return new ErrorEventWireModel(attribValues, isSynthetics, priority);
        }

        private IList<string> GetFormattedStackTrace(ErrorData errorData)
        {
            if (errorData.StackTrace == null)
            {
                return null;
            }

            var stackTrace = StackTraces.ScrubAndTruncate(errorData.StackTrace, _configurationService.Configuration.StackTraceMaximumFrames);
            return stackTrace;
        }

        private void SetErrorGroup(ErrorData errorData, IAttributeValueCollection attribValues)
        {
            if (_configurationService.Configuration.ErrorGroupCallback == null)
            {
                return;
            }

            var callbackAttributes = attribValues.GetAllAttributeValuesDic();
            if (errorData?.RawException != null)
            {
                callbackAttributes[ExceptionAttributeName] = errorData.RawException;
            }

            var stackTrace = GetFormattedStackTrace(errorData);
            if (stackTrace != null && stackTrace.Count > 0)
            {
                callbackAttributes[StackTraceAttributeName] = stackTrace;
            }

            using (_agentTimerService.StartNew(SetErrorGroupSupportabilityName))
            {
                var errorGroup = _configurationService.Configuration.ErrorGroupCallback?.Invoke((IReadOnlyDictionary<string, object>)callbackAttributes);
                _attribDefs.ErrorGroup.TrySetValue(attribValues, errorGroup);
            }
        }
    }
}
