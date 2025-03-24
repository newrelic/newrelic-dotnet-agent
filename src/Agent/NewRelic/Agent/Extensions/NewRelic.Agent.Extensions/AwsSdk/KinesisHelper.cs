// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using NewRelic.Reflection;

namespace NewRelic.Agent.Extensions.AwsSdk
{
    public static class KinesisHelper
    {

        private static readonly ConcurrentDictionary<string, string> _streamNameCache = new();
        private static readonly ConcurrentDictionary<string, Func<object, string>> _propertyGetterCache = new();

        public static string GetStreamNameFromRequest(object request)
        {
            var streamName = GetPropertyFromRequest(request, "StreamName");
            if (streamName != null)
            {
                return streamName;
            }
            // if StreamName is null/unavailable, StreamARN may exist
            var streamARN = GetStreamArnFromRequest(request);
            if (streamARN != null)
            {
                return GetStreamNameFromArn(streamARN);
            }
            return null;
        }


        public static string GetDeliveryStreamNameFromRequest(object request)
        {
            var streamName = GetPropertyFromRequest(request, "DeliveryStreamName");
            if (streamName != null)
            {
                return streamName;
            }
            // if StreamName is null/unavailable, StreamARN may exist
            var streamARN = GetDeliveryStreamArnFromRequest(request);
            if (streamARN != null)
            {
                return GetStreamNameFromArn(streamARN);
            }
            return null;
        }

        public static string GetStreamArnFromRequest(object request)
        {
            return GetPropertyFromRequest(request, "StreamARN");
        }

        public static string GetDeliveryStreamArnFromRequest(object request)
        {
            return GetPropertyFromRequest(request, "DeliveryStreamARN");
        }

        public static string GetStreamNameFromArn(string streamARN)
        {
            // arn:aws:kinesis:us-west-2:111111111111:deliverystream/NameOfStream
            if (_streamNameCache.ContainsKey(streamARN))
            {
                return _streamNameCache[streamARN];
            }
            else
            {
                var arnParts = streamARN.Split(':');
                if (arnParts.Length > 1)
                {
                    var lastPart = arnParts[arnParts.Length - 1];
                    if (lastPart.Contains('/'))
                    {
                        var streamName = lastPart.Split('/')[1];
                        return _streamNameCache[streamARN] = streamName;
                    }
                }
                return null;
            }
        }

        private static string GetPropertyFromRequest(object request, string propertyName)
        {
            Type type = request.GetType();
            var key = type.Name + propertyName;
            var getter = _propertyGetterCache.GetOrAdd(key, GetPropertyAccessor(type, propertyName));
            return getter(request);
        }

        private static Func<object, string> GetPropertyAccessor(Type type, string propertyName)
        {
            try
            {
                return VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(Assembly.GetAssembly(type).FullName, type.FullName, propertyName);
            }
            catch
            {
                // if the attempt to generate the property accessor fails, that means that the requested property name does not exist for this particular
                // Kinesis/Firehose request type.  Return a delegate that always returns null for any object input 
                return (o) => null;
            }
        }

    }
}
