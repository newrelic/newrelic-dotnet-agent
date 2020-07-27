using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utils;
using NewRelic.Agent.Core.WireModels;
using NewRelic.SystemExtensions;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
    public interface IErrorTraceMaker
    {
        /// <summary>
        /// Returns the best possible error trace for a given transaction.
        /// </summary>
        ErrorTraceWireModel GetErrorTrace(ImmutableTransaction immutableTransaction, Attributes transactionAttributes, TransactionMetricName transactionMetricName, ErrorData errorData);

        /// <summary>
        /// Returns an error trace for the given custom error data. 
        /// </summary>
        ErrorTraceWireModel GetErrorTrace(Attributes customAttributes, ErrorData errorData);
    }

    public class ErrorTraceMaker : IErrorTraceMaker
    {
        private readonly IConfigurationService _configurationService;
        private readonly IAttributeService _attributeService;

        public ErrorTraceMaker(IConfigurationService configurationService, IAttributeService attributeService)
        {
            _configurationService = configurationService;
            _attributeService = attributeService;
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
        /// <param name="customAttributes"></param>
        /// <param name="errorData"></param>
        /// <returns></returns>
        public ErrorTraceWireModel GetErrorTrace(Attributes customAttributes, ErrorData errorData)
        {
            var uri = string.Empty;
            var stackTrace = GetFormattedStackTrace(errorData);

            var timestamp = errorData.NoticedAt;
            const string path = "NewRelic.Api.Agent.NoticeError API Call";
            var message = GetStrippedErrorMessage(errorData.ErrorMessage);
            var exceptionClassName = errorData.ErrorTypeName;
            var errorAttributesWireModel = GetErrorTraceAttributes(uri, customAttributes, stackTrace);
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
        public ErrorTraceWireModel GetErrorTrace(ImmutableTransaction immutableTransaction, Attributes transactionAttributes, TransactionMetricName transactionMetricName, ErrorData errorData)
        {
            var uri = immutableTransaction.TransactionMetadata.Uri?.TrimAfter("?");
            var stackTrace = GetFormattedStackTrace(errorData);

            var timestamp = errorData.NoticedAt;
            var path = transactionMetricName.PrefixedName;
            var message = GetStrippedErrorMessage(errorData.ErrorMessage);
            var exceptionClassName = errorData.ErrorTypeName;
            var errorAttributesWireModel = GetErrorTraceAttributes(uri ?? string.Empty, transactionAttributes, stackTrace);
            var guid = immutableTransaction.Guid;

            return new ErrorTraceWireModel(timestamp, path, message, exceptionClassName, errorAttributesWireModel, guid);
        }

        private IEnumerable<string> GetFormattedStackTrace(ErrorData errorData)
        {
            if (errorData.StackTrace == null)
            {
                return null;
            }

            var stackTrace = StackTraces.ScrubAndTruncate(errorData.StackTrace, _configurationService.Configuration.StackTraceMaximumFrames);
            return stackTrace;
        }

        private string GetStrippedErrorMessage(string errorMessage)
        {
            if (_configurationService.Configuration.HighSecurityModeEnabled)
            {
                return null;
            }

            return errorMessage;
        }
        private ErrorTraceWireModel.ErrorTraceAttributesWireModel GetErrorTraceAttributes(string uri, Attributes attributes, IEnumerable<string> stackTrace)
        {
            var filteredAttributes = _attributeService.FilterAttributes(attributes, AttributeDestinations.ErrorTrace);
            var agentAttributes = filteredAttributes.GetAgentAttributesDictionary();
            var intrinsicAttributes = filteredAttributes.GetIntrinsicsDictionary();
            var userAttributes = filteredAttributes.GetUserAttributesDictionary();

            return new ErrorTraceWireModel.ErrorTraceAttributesWireModel(uri, agentAttributes, intrinsicAttributes, userAttributes, stackTrace);
        }
    }
}
