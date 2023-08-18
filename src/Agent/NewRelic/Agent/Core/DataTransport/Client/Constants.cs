// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.DataTransport.Client
{
    /// <summary>
    /// Constants shared between implementations of IHttpClient
    /// </summary>
    public static class Constants
    {
        public const string EmptyResponseBody = "{}";
        public const int CompressMinimumByteLength = 20;
        public const int ProtocolVersion = 17;
        public const string LicenseKeyParameterName = "license_key";

    }
}
