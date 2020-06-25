/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Core.Errors;

namespace NewRelic.Agent.Core.Segments
{
    public interface IInternalSpan : ISegment, ISegmentExperimental, ISpan
    {
        ErrorData ErrorData { get; set; }
    }
}
