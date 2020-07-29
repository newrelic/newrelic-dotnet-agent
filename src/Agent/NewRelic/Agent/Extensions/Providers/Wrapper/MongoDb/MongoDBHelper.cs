/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using MongoDB.Driver;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.MongoDb
{
    public static class MongoDBHelper
    {
        public static string GetCollectionModelName(MethodCall methodCall)
        {
            var collection = methodCall.InvocationTarget as MongoCollection;
            if (collection == null)
                throw new Exception("Method's invocation target is not a MongoCollection.");
            return collection.Name;
        }
    }
}
