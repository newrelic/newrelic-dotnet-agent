// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Providers.Wrapper.Elasticsearch
{
    public class ElasticsearchRequestException : Exception
    {
        public ElasticsearchRequestException(string message)
            : base(message) { }

        public ElasticsearchRequestException()
            : base() { }

        public ElasticsearchRequestException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
