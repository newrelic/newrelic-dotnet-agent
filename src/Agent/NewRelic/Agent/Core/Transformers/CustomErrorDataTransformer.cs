// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;

namespace NewRelic.Agent.Core.Transformers;

public interface ICustomErrorDataTransformer
{
    void Transform(ErrorData errorData, float priority, string userid);
}

public class CustomErrorDataTransformer : ICustomErrorDataTransformer
{
    private readonly IConfigurationService _configurationService;

    private readonly IAttributeDefinitionService _attribDefSvc;
    private IAttributeDefinitions _attribDefs => _attribDefSvc?.AttributeDefs;

    private readonly IErrorTraceMaker _errorTraceMaker;

    private readonly IErrorEventMaker _errorEventMaker;

    private readonly IErrorTraceAggregator _errorTraceAggregator;

    private readonly IErrorEventAggregator _errorEventAggregator;

    public CustomErrorDataTransformer(IConfigurationService configurationService, 
        IAttributeDefinitionService attribDefSvc,
        IErrorTraceMaker errorTraceMaker, IErrorTraceAggregator errorTraceAggregator,
        IErrorEventMaker errorEventMaker, IErrorEventAggregator errorEventAggregator)
    {
        _configurationService = configurationService;
        _attribDefSvc = attribDefSvc;
        _errorTraceMaker = errorTraceMaker;
        _errorTraceAggregator = errorTraceAggregator;
        _errorEventMaker = errorEventMaker;
        _errorEventAggregator = errorEventAggregator;
    }

    public void Transform(ErrorData errorData, float priority, string userid)
    {
        if (!_configurationService.Configuration.ErrorCollectorEnabled)
        {
            return;
        }

        var attribValues = new AttributeValueCollection(AttributeDestinations.ErrorEvent, AttributeDestinations.ErrorTrace);

        if (errorData.CustomAttributes != null && _configurationService.Configuration.CaptureCustomParameters)
        {
            foreach (var customAttrib in errorData.CustomAttributes)
            {
                _attribDefs.GetCustomAttributeForError(customAttrib.Key).TrySetValue(attribValues, customAttrib.Value);
            }
        }

        // For Custom Errors (occurring outside a transaction), UI Error Analytics page co-opts the
        // 'transactionName' attribute to find the corresponding Error Trace (matching it to 'Path') 
        // so it can display the stack trace. 
        _attribDefs.TransactionNameForError.TrySetValue(attribValues, errorData.Path);

        if (!string.IsNullOrWhiteSpace(userid))
        {
            _attribDefs.EndUserId.TrySetValue(attribValues, userid);
        }

        //We have to do the filtering here b/c these methods further update
        var errorTrace = _errorTraceMaker.GetErrorTrace(new AttributeValueCollection(attribValues, AttributeDestinations.ErrorTrace), errorData);
        var errorEvent = _errorEventMaker.GetErrorEvent(errorData, new AttributeValueCollection(attribValues, AttributeDestinations.ErrorEvent), priority);

        _errorTraceAggregator.Collect(errorTrace);
        _errorEventAggregator.Collect(errorEvent);
    }
}
