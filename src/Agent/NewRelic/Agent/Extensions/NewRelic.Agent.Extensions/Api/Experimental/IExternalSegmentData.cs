// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Api.Experimental
{
    /// <summary>
    /// This interface contains methods we may eventually move out of the experimental namespace once they have been sufficiently vetted.
    /// Methods on this interface are subject to refactoring or removal in future versions of the API.
    /// </summary>
    public interface IExternalSegmentData : ISegmentData
    {
        void SetHttpStatus(int httpStatusCode, string httpStatusText = null);
    }
}
