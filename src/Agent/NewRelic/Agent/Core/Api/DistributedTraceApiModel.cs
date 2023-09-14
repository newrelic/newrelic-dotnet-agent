// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Core;
using NewRelic.Core.Logging;
using System;

namespace NewRelic.Agent.Core.Api
{
    public class DistributedTraceApiModel : IDistributedTracePayload
    {
        public static readonly DistributedTraceApiModel EmptyModel = new DistributedTraceApiModel(string.Empty);

        private readonly Lazy<string> _text;
        private bool _isEmpty = true;
        private string _httpSafe = string.Empty;

        public DistributedTraceApiModel(string encodedPayload)
        {
            _httpSafe = encodedPayload;
            _text = new Lazy<string>(DecodePayload);
            _isEmpty = string.IsNullOrEmpty(_httpSafe);

            string DecodePayload()
            {
                try
                {
                    using (new IgnoreWork())
                    {
                        return Strings.Base64Decode(encodedPayload);
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        Log.Error(ex, "Failed to get DistributedTraceApiModel.Text");
                    }
                    catch (Exception)
                    {
                        //Swallow the error
                    }
                    return string.Empty;
                }
            }
        }

        public string HttpSafe()
        {
            return _httpSafe;
        }

        public string Text()
        {
            return _text.Value;
        }

        public bool IsEmpty()
        {
            return _isEmpty;
        }
    }
}
