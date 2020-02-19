using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;

namespace NewRelic.Agent.Core.Transformers
{
	public interface ICustomErrorDataTransformer
	{
		void Transform<T>(ErrorData errorData, IEnumerable<KeyValuePair<string, T>> customAttributes, float priority);
	}

	public class CustomErrorDataTransformer : ICustomErrorDataTransformer
	{
		private readonly IConfigurationService _configurationService;

		private readonly IAttributeService _attributeService;

		private readonly IErrorTraceMaker _errorTraceMaker;

		private readonly IErrorEventMaker _errorEventMaker;

		private readonly IErrorTraceAggregator _errorTraceAggregator;

		private readonly IErrorEventAggregator _errorEventAggregator;

		public CustomErrorDataTransformer(IConfigurationService configurationService, IAttributeService attributeService, 
			IErrorTraceMaker errorTraceMaker, IErrorTraceAggregator errorTraceAggregator,
			IErrorEventMaker errorEventMaker, IErrorEventAggregator errorEventAggregator)
		{
			_configurationService = configurationService;
			_attributeService = attributeService;
			_errorTraceMaker = errorTraceMaker;
			_errorTraceAggregator = errorTraceAggregator;
			_errorEventMaker = errorEventMaker;
			_errorEventAggregator = errorEventAggregator;
		}

		public void Transform<T>(ErrorData errorData, IEnumerable<KeyValuePair<string, T>> customAttributes, float priority)
		{
			if (!_configurationService.Configuration.ErrorCollectorEnabled)
				return;

			var errorEventAttributes = new AttributeCollection();
			var errorTraceAttributes = new AttributeCollection();

			if (customAttributes != null && _configurationService.Configuration.CaptureCustomParameters)
			{
				errorEventAttributes.TryAddAll(Attribute.BuildCustomAttributeForError, customAttributes);
				errorTraceAttributes.TryAddAll(Attribute.BuildCustomAttributeForError, customAttributes);
			}

			// For Custom Errors (occurring outside a transaction), UI Error Analytics page co-opts the
			// 'transactionName' attribute to find the corresponding Error Trace (matching it to 'Path') 
			// so it can display the stack trace. 
			errorEventAttributes.Add(Attribute.BuildTransactionNameAttributeForCustomError(errorData.Path));

			errorEventAttributes = _attributeService.FilterAttributes(errorEventAttributes, AttributeDestinations.ErrorEvent);
			errorTraceAttributes = _attributeService.FilterAttributes(errorTraceAttributes, AttributeDestinations.ErrorTrace);

			var errorTrace = _errorTraceMaker.GetErrorTrace(errorTraceAttributes, errorData);
			var errorEvent = _errorEventMaker.GetErrorEvent(errorData, errorEventAttributes, priority);

			_errorTraceAggregator.Collect(errorTrace);
			_errorEventAggregator.Collect(errorEvent);
		}
	}
}
