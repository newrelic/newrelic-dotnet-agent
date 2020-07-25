using System;
using System.Collections.Generic;
using System.Linq;
using MoreLinq;
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
        void Transform(ErrorData errorData, IEnumerable<KeyValuePair<String, String>> customAttributes = null);
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

        public void Transform(ErrorData errorData, IEnumerable<KeyValuePair<String, String>> customAttributes = null)
        {
            if (!_configurationService.Configuration.ErrorCollectorEnabled)
                return;

            var errorEventAttributes = new Attributes();
            var errorTraceAttributes = new Attributes();

            customAttributes?
                .Where(attr => attr.Key != null && attr.Value != null)
                .Select(attr => Attribute.BuildCustomAttribute(attr.Key, attr.Value))
                .ForEach(attr =>
                {
                    errorEventAttributes.Add(attr);
                    errorTraceAttributes.Add(attr);
                });

            errorEventAttributes = _attributeService.FilterAttributes(errorEventAttributes, AttributeDestinations.ErrorEvent);
            errorTraceAttributes = _attributeService.FilterAttributes(errorTraceAttributes, AttributeDestinations.ErrorTrace);

            var errorTrace = _errorTraceMaker.GetErrorTrace(errorTraceAttributes, errorData);
            var errorEvent = _errorEventMaker.GetErrorEvent(errorData, errorEventAttributes);

            _errorTraceAggregator.Collect(errorTrace);
            _errorEventAggregator.Collect(errorEvent);
        }
    }
}
