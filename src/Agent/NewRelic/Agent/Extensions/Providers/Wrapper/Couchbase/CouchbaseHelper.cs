// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Couchbase
{
    public static class CouchbaseHelper
    {
        private static Func<object, string> _getBucketName;
        private static Func<object, string> _getStatement;

        public static string GetBucketName(object owner)
        {
            var nameGetter = _getBucketName ?? (_getBucketName = VisibilityBypasser.Instance.GeneratePropertyAccessor<string>("Couchbase.NetClient", "Couchbase.CouchbaseBucket", "Name"));
            return nameGetter(owner);
        }

        public static string GetStatement(object parm, string parameterTypeName)
        {
            if (parm is string s)
            {
                return s;
            }

            switch (parameterTypeName)
            {
                case ("Couchbase.N1QL.IQueryRequest"):
                    {
                        var statementGetter = _getStatement ?? (_getStatement = VisibilityBypasser.Instance.GenerateFieldReadAccessor<string>("Couchbase.NetClient", "Couchbase.N1QL.QueryRequest", "_statement"));
                        return statementGetter(parm);
                    }
                // Couchbase.CouchbaseBucket.Query() and Couchbase.CouchbaseBucket.QueryAsync() support the following parameters in addition to string and IQueryRequest.
                // At this time we are not supplying any sort of data for the query text for these parameter types. Returning null for these cases will result in no sql 
                // trace being reported, but metrics will be reported. 
                case ("Couchbase.Search.SearchQuery"):
                case ("Couchbase.Views.IViewQueryable"):
                default:
                    {
                        return null;
                    }
            }
        }
    }
}
