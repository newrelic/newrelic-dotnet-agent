using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using Attribute = NewRelic.Agent.Core.Transactions.Attribute;

namespace NewRelic.Agent.Core.Transformers
{
	public interface ICustomErrorDataTransformer
	{
		void Transform(ErrorData errorData, IEnumerable<KeyValuePair<string, string>> customAttributes, float priority);
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

		public void Transform(ErrorData errorData, IEnumerable<KeyValuePair<string, string>> customAttributes, float priority)
		{
			if (!_configurationService.Configuration.ErrorCollectorEnabled)
				return;

			var errorEventAttributes = new Attributes();
			var errorTraceAttributes = new Attributes();

			if (customAttributes != null && _configurationService.Configuration.CaptureCustomParameters)
			{
				foreach(var customAttr in customAttributes)
				{
					if ( customAttr.Key != null && customAttr.Value != null)
					{
						var attr = Attribute.BuildCustomAttribute(customAttr.Key, customAttr.Value);
						errorEventAttributes.Add(attr);
						errorTraceAttributes.Add(attr);
					}
				}
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