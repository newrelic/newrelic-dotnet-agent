// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.SimpleNotificationService.Model;
using Amazon.SQS.Model;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace NewRelic.OpenTracing.AmazonLambda.Helpers
{
    internal static class ServiceExtensions
    {
        private const string PhoneNumber = "PhoneNumber";

        #region SNS

        internal static KeyValuePair<string, string> GetSNSOperationData(this HttpRequestMessage httpRequestMessage)
        {
            var snsbody = httpRequestMessage.Content.ReadAsStringAsync().Result;
            var operation = OperationMapper.GetSNSOperation(snsbody);
            var operationName = GetSNSOperationName(snsbody, operation);
            return new KeyValuePair<string, string>(operationName, operation);
        }

        // Parse Body to retrieve TopicName
        // Looks like this
        // Body: "Action=Publish&Message=Test%20Message&TopicArn=arn%3Aaws%3Asns%3Aus-west-2%3A342444490463%3ADotNetTestSNSTopic&Version=2010-03-31"

        private static string GetSNSOperationName(string body, string operation)
        {
            var snsSplit = body.Split('&');
            if (snsSplit.Length != 4)
            {
                return null;
            }

            var action = snsSplit[0].Split('=');
            if (action.Length != 2)
            {
                return null;
            }

            string topicOrPhoneNumber;
            if (snsSplit[2].StartsWith(PhoneNumber))
            {
                topicOrPhoneNumber = PhoneNumber;
            }
            else
            {
                topicOrPhoneNumber = GetTopicOrTargetName(snsSplit[2]);
            }

            if (string.IsNullOrEmpty(topicOrPhoneNumber))
            {
                return null;
            }

            return string.Format("MessageBroker/SNS/Topic/{0}/Named/{1}", operation, topicOrPhoneNumber);
        }

        internal static string GetOperationName(this PublishRequest publishRequest, string operation)
        {
            if (publishRequest == null)
            {
                return null;
            }

            string topicOrPhoneNumber = null;
            if (!string.IsNullOrEmpty(publishRequest.PhoneNumber))
            {
                topicOrPhoneNumber = PhoneNumber;
            }
            else
            {
                var arn = publishRequest.TopicArn ?? publishRequest.TargetArn;
                topicOrPhoneNumber = GetTopicOrTargetName(arn);
            }

            if (string.IsNullOrEmpty(topicOrPhoneNumber))
            {
                return null;
            }

            return string.Format("MessageBroker/SNS/Topic/{0}/Named/{1}", operation, topicOrPhoneNumber);
        }

        private static string GetTopicOrTargetName(string arn)
        {

            if (string.IsNullOrEmpty(arn))
            {
                return null;
            }

            var splitArn = arn.Split(new[] { ":", "%3A" }, StringSplitOptions.None);
            if (splitArn.Length < 6)
            {
                return null;
            }

            return splitArn[5];
        }

        #endregion

        #region SQS

        // Content Looks like this:
        // "Action=SendMessage&DelaySeconds=5&MessageBody=John%20Doe%20customer%20information.&Version=2012-11-05"	

        internal static string GetOperationName(this SendMessageRequest sendMessageRequest, string operation)
        {
            return GetSQSOperationNameImpl(sendMessageRequest.QueueUrl, operation);
        }

        internal static string GetOperationName(this SendMessageBatchRequest sendMessageBatchRequest, string operation)
        {
            return GetSQSOperationNameImpl(sendMessageBatchRequest.QueueUrl, operation);
        }

        internal static KeyValuePair<string, string> GetSQSOperationData(this HttpRequestMessage httpRequestMessage)
        {
            var sqsBody = httpRequestMessage.Content.ReadAsStringAsync().Result;
            var operation = OperationMapper.GetSQSOperation(sqsBody);
            var operationName = GetSQSOperationNameImpl(httpRequestMessage.RequestUri.ToString(), operation);
            return new KeyValuePair<string, string>(operationName, operation);
        }

        private static string GetSQSOperationNameImpl(string queueUrl, string operation)
        {
            var uriSplit = queueUrl.Split(new[] { "/" }, StringSplitOptions.None);
            if (uriSplit.Length < 5 || string.IsNullOrEmpty(uriSplit[4]))
            {
                return null;
            }

            return string.Format("MessageBroker/SQS/Queue/{0}/Named/{1}", operation, uriSplit[4]);
        }

        #endregion

        #region DynamoDB

        internal static KeyValuePair<string, string> GetDynamoDBOperationData(this HttpRequestMessage httpRequestMessage)
        {
            var operation = OperationMapper.GetDynamoDBOperation((httpRequestMessage));
            var dynamoDBBody = httpRequestMessage.Content.ReadAsStringAsync().Result;
            // Body is of the following Format
            //"{\"TableName\":\"DotNetTest\"}"
            // "{\"AttributesToGet\":[\"Name\",\"Horse\"],\"ConsistentRead\":true,\"Key\":{\"Key\":{\"S\":\"Name\"}},\"TableName\":\"DotNetTest\"}"
            var bodyJson = JObject.Parse(dynamoDBBody);
            var operationName = string.Format("Datastore/statement/DynamoDB/{0}/{1}", bodyJson?["TableName"], operation);
            return new KeyValuePair<string, string>(operationName, operation);
        }

        #endregion
    }
}
