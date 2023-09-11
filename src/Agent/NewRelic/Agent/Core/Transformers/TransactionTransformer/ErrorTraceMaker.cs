// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
    public interface IErrorTraceMaker
    {
        /// <summary>
        /// Returns the best possible error trace for a given transaction.
        /// </summary>
        ErrorTraceWireModel GetErrorTrace(ImmutableTransaction immutableTransaction, IAttributeValueCollection attributeValues, TransactionMetricName transactionMetricName);

        /// <summary>
        /// Returns an error trace for the given custom error data. 
        /// </summary>
        ErrorTraceWireModel GetErrorTrace(IAttributeValueCollection attributeValues, ErrorData errorData);
    }

    public class ErrorTraceMaker : IErrorTraceMaker
    {
        private const string SetErrorGroupSupportabilityName = "ErrorTraceMakerSetErrorGroup";
        private const string ExceptionAttributeName = "exception";
        private const string StackTraceAttributeName = "stack_trace";

        private readonly IConfigurationService _configurationService;
        private readonly IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc.AttributeDefs;
        private readonly IAgentTimerService _agentTimerService;

        public ErrorTraceMaker(IConfigurationService configurationService, IAttributeDefinitionService attributeService, IAgentTimerService agentTimerService)
        {
            _configurationService = configurationService;
            _attribDefSvc = attributeService;
            _agentTimerService = agentTimerService;
        }

        /// <summary>
        /// Gets an <see cref="NewRelic.Agent.Core.WireModels.ErrorTraceWireModel"/> given
        /// attributes and an error referenced by an <see cref="NewRelic.Agent.Core.Errors.ErrorData"/> 
        /// occurring outside of a transaction.
        /// </summary>
        /// <remarks>
        /// The <param name="errorData"></param> passed to this method is assumed to contain valid error information.
        /// The method won't throw if it is not but will send meaningless data in some of the attributes.
        /// </remarks>
        /// <param name="attribValues"></param>
        /// <param name="errorData"></param>
        /// <returns></returns>
        public ErrorTraceWireModel GetErrorTrace(IAttributeValueCollection attribValues, ErrorData errorData)
        {
            var stackTrace = GetFormattedStackTrace(errorData);

            var timestamp = errorData.NoticedAt;
            var path = errorData.Path;
            var message = errorData.ErrorMessage;
            var exceptionClassName = errorData.ErrorTypeName;
            SetErrorGroup(errorData, stackTrace, attribValues);
            var errorAttributesWireModel = GetErrorTraceAttributes(attribValues, stackTrace);
            const string guid = null;

            return new ErrorTraceWireModel(timestamp, path, message, exceptionClassName, errorAttributesWireModel, guid);
        }

        /// <summary>
        /// Gets an <see cref="NewRelic.Agent.Core.WireModels.ErrorTraceWireModel"/> given
        /// a transaction, transaction attributes and an error referenced by an <see cref="NewRelic.Agent.Core.Errors.ErrorData"/>
        /// occurring inside of a transaction.
        /// </summary>
        /// <remarks>
        /// The <param name="errorData"></param> passed to this method is assumed to contain valid error information.
        /// The method won't throw if it is not but will send meaningless data in some of the attributes.
        /// </remarks>
        /// <param name="immutableTransaction"></param>
        /// <param name="transactionAttributes"></param>
        /// <param name="transactionMetricName"></param>
        /// <param name="errorData"></param>
        /// <returns></returns>
        public ErrorTraceWireModel GetErrorTrace(ImmutableTransaction immutableTransaction, IAttributeValueCollection transactionAttributes, TransactionMetricName transactionMetricName)
        {
            var errorData = immutableTransaction.TransactionMetadata.ReadOnlyTransactionErrorState.ErrorData;

            var stackTrace = GetFormattedStackTrace(errorData);

            var timestamp = errorData.NoticedAt;
            var path = transactionMetricName.PrefixedName;
            var message = errorData.ErrorMessage;
            var exceptionClassName = errorData.ErrorTypeName;
            SetErrorGroup(errorData, stackTrace, transactionAttributes);
            var errorAttributesWireModel = GetErrorTraceAttributes(transactionAttributes, stackTrace);
            var guid = immutableTransaction.Guid;

            return new ErrorTraceWireModel(timestamp, path, message, exceptionClassName, errorAttributesWireModel, guid);
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

        private ErrorTraceWireModel.ErrorTraceAttributesWireModel GetErrorTraceAttributes(IAttributeValueCollection attributes, IList<string> stackTrace)
        {
            return new ErrorTraceWireModel.ErrorTraceAttributesWireModel(attributes, stackTrace);
        }

        private void SetErrorGroup(ErrorData errorData, IList<string> stackTrace, IAttributeValueCollection attribValues)
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
