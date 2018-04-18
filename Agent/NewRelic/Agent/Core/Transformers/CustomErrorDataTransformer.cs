using System;
using System.Collections.Generic;
using JetBrains.Annotations;
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
		void Transform(ErrorData errorData, [CanBeNull] IEnumerable<KeyValuePair<String, String>> customAttributes = null);
	}

	public class CustomErrorDataTransformer : ICustomErrorDataTransformer
	{
		[NotNull]
		private readonly IConfigurationService _configurationService;

		[NotNull]
		private readonly IAttributeService _attributeService;

		[NotNull]
		private readonly IErrorTraceMaker _errorTraceMaker;

		[NotNull]
		private readonly IErrorEventMaker _errorEventMaker;

		[NotNull]
		private readonly IErrorTraceAggregator _errorTraceAggregator;

		[NotNull]
		private readonly IErrorEventAggregator _errorEventAggregator;

		public CustomErrorDataTransformer([NotNull] IConfigurationService configurationService, [NotNull] IAttributeService attributeService, 
			[NotNull] IErrorTraceMaker errorTraceMaker, [NotNull] IErrorTraceAggregator errorTraceAggregator,
			[NotNull] IErrorEventMaker errorEventMaker, [NotNull] IErrorEventAggregator errorEventAggregator)
		{
			_configurationService = configurationService;
			_attributeService = attributeService;
			_errorTraceMaker = errorTraceMaker;
			_errorTraceAggregator = errorTraceAggregator;
			_errorEventMaker = errorEventMaker;
			_errorEventAggregator = errorEventAggregator;
		}

		public void Transform(ErrorData errorData, IEnumerable<KeyValuePair<String, String>> customAttributes = null)
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

			errorEventAttributes = _attributeService.FilterAttributes(errorEventAttributes, AttributeDestinations.ErrorEvent);
			errorTraceAttributes = _attributeService.FilterAttributes(errorTraceAttributes, AttributeDestinations.ErrorTrace);

			var errorTrace = _errorTraceMaker.GetErrorTrace(errorTraceAttributes, errorData);
			var errorEvent = _errorEventMaker.GetErrorEvent(errorData, errorEventAttributes);

			_errorTraceAggregator.Collect(errorTrace);
			_errorEventAggregator.Collect(errorEvent);
		}
	}
}