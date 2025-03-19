// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Extensions.AwsSdk
{
    public static class KinesisHelper
    {

        private static readonly ConcurrentDictionary<string, string> _streamNameCache = new();
        private static readonly ConcurrentDictionary<Type, List<string>> _propertyInfoCache = new();

        public static string GetStreamNameFromRequest(dynamic request)
        {
            try
            {
                var streamName = GetPropertyFromDynamic(request, "StreamName");
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
            }
            catch
            {
            }
            return null;
        }


        public static string GetDeliveryStreamNameFromRequest(dynamic request)
        {
            try
            {
                var streamName = GetPropertyFromDynamic(request, "DeliveryStreamName");
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
            }
            catch
            {
            }
            return null;
        }

        public static string GetStreamArnFromRequest(dynamic request)
        {
            return GetPropertyFromDynamic(request, "StreamARN");
        }

        public static string GetDeliveryStreamArnFromRequest(dynamic request)
        {
            return GetPropertyFromDynamic(request, "DeliveryStreamARN");
        }

        private static string GetPropertyFromDynamic(dynamic request, string propertyName)
        {
            Type type = request.GetType();
            List<string> properties = _propertyInfoCache.ContainsKey(type) ? _propertyInfoCache[type] : _propertyInfoCache[type] = type.GetProperties().Select(p => p.Name).ToList();
            return properties.Contains(propertyName) ? type.GetProperty(propertyName).GetValue(request) : null;
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

    }
}
