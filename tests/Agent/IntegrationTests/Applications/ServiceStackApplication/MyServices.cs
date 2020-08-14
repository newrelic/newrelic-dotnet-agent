// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using ServiceStack;

namespace ServiceStackApplication
{
    public class MyServices : Service
    {
        public object Any(Hello request)
        {
            return new HelloResponse { Result = $"{request.Name}" };
        }
    }
}
