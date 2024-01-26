// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.Lambda.KinesisEvents;
using Amazon.Lambda.KinesisFirehoseEvents;
using Amazon.Lambda.S3Events;
using Amazon.Lambda.SNSEvents;
using Amazon.Lambda.SQSEvents;
using NewRelic.OpenTracing.AmazonLambda;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net;

namespace NewRelic.Tests.AwsLambda.AwsLambdaWrapperTests
{
    [TestFixture]
    public class IOParserTests
    {
        private const string TestArn = "test-arn";

        private IDictionary<string, string> _singleValueHeaders;
        private IDictionary<string, string> _tags;
        private IDictionary<string, IList<string>> _multiValueHeaders;
        private APIGatewayProxyRequest _baseAPIGatewayProxyRequest;
        private APIGatewayProxyResponse _baseAPIGatewayProxyResponse;
        private ApplicationLoadBalancerRequest _baseApplicationLoadBalancerRequest;
        private ApplicationLoadBalancerResponse _baseApplicationLoadBalancerResponse;
        private SQSEvent _baseSQSEvent;
        private SNSEvent _baseSNSEvent;
        private KinesisEvent _baseKinesisEvent;
        private S3Event _baseS3Event;
        private DynamoDBEvent _baseDynamoDBEvent;
        private KinesisFirehoseEvent _baseKinesisFirehoseEvent;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _singleValueHeaders = new Dictionary<string, string>
            {
                { "X-Forwarded-Port","1234" },
                { "X-Forwarded-Proto","proto" },
                { "Content-Type","application/json" },
                { "Content-Length","1000" },
                { "Access-Control-Allow-Origin", "*" },
                { "Custom-Header","CustomValue" },
                { "newrelic","dt-payload" },
            };

            _multiValueHeaders = new Dictionary<string, IList<string>>
            {
                { "X-Forwarded-Port", new[]{ "1234", "4321" } },
                { "X-Forwarded-Proto",new[]{ "proto" } },
                { "Content-Type", new[]{ "application/json" } },
                { "Content-Length", new[]{ "1000" } },
                { "Access-Control-Allow-Origin", new[]{ "*" } },
                { "Custom-Header", new[]{ "CustomValue1", "CustomValue2" } },
                { "newrelic", new[]{ "dt-payload" } },
            };

            _tags = new Dictionary<string, string>
            {
                { "testTag","1234" },
            };
        }

        [SetUp]
        public void Setup()
        {

            // APIGatewayProxy
            _baseAPIGatewayProxyRequest = new APIGatewayProxyRequest
            {
                HttpMethod = "POST",
                Path = "/test/path",
            };

            _baseAPIGatewayProxyResponse = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
            };

            // ApplicationLoadBalancer
            var albRequestContext = new ApplicationLoadBalancerRequest.ALBRequestContext
            {
                Elb = new ApplicationLoadBalancerRequest.ElbInfo()
            };
            albRequestContext.Elb.TargetGroupArn = TestArn;
            _baseApplicationLoadBalancerRequest = new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Path = "/test/path",
                RequestContext = albRequestContext,
            };

            _baseApplicationLoadBalancerResponse = new ApplicationLoadBalancerResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
            };

            // SQSEvent
            var sqsRecord = new SQSEvent.SQSMessage
            {
                EventSourceArn = TestArn
            };
            _baseSQSEvent = new SQSEvent
            {
                Records = new List<SQSEvent.SQSMessage> { sqsRecord },
            };

            // SNSEvent
            var snsMessaage = new SNSEvent.SNSMessage()
            {
                Message = "Test Message",
            };
            var snsRecord = new SNSEvent.SNSRecord
            {
                EventSubscriptionArn = TestArn,
                Sns = snsMessaage
            };
            _baseSNSEvent = new SNSEvent
            {
                Records = new List<SNSEvent.SNSRecord> { snsRecord },
            };

            // KinesisEvent
            var kinesisRecord = new KinesisEvent.KinesisEventRecord
            {
                EventSourceARN = TestArn
            };
            _baseKinesisEvent = new KinesisEvent
            {
                Records = new List<KinesisEvent.KinesisEventRecord> { kinesisRecord },
            };

            // S3Event
            var s3Record = new Amazon.S3.Util.S3EventNotification.S3EventNotificationRecord
            {
                S3 = new Amazon.S3.Util.S3EventNotification.S3Entity
                {
                    Bucket = new Amazon.S3.Util.S3EventNotification.S3BucketEntity
                    {
                        Arn = TestArn
                    }
                }
            };
            _baseS3Event = new S3Event
            {
                Records = new List<Amazon.S3.Util.S3EventNotification.S3EventNotificationRecord> { s3Record },
            };

            // DynamoDBEvent
            var dynamoDBRecord = new DynamoDBEvent.DynamodbStreamRecord
            {
                EventSourceArn = TestArn
            };
            _baseDynamoDBEvent = new DynamoDBEvent
            {
                Records = new List<DynamoDBEvent.DynamodbStreamRecord> { dynamoDBRecord },
            };

            // KinesisFirehoseEvent
            _baseKinesisFirehoseEvent = new KinesisFirehoseEvent
            {
                DeliveryStreamArn = TestArn,
            };
        }



        #region APIGatewayProxy

        [Test]
        public void ParseRequest_APIGatewayProxyRequest_WithoutHeaders()
        {
            var tags = IOParser.ParseRequest(_baseAPIGatewayProxyRequest);

            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("method"), Is.True);
                Assert.That(tags.Keys.Contains("uri"), Is.True);

                Assert.That(tags["method"], Is.EqualTo("POST"));
                Assert.That(tags["uri"], Is.EqualTo("/test/path"));
            });
        }

        [Test]
        public void ParseRequest_APIGatewayProxyRequest_WithSingleValueHeaders()
        {
            _baseAPIGatewayProxyRequest.Headers = _singleValueHeaders;
            var tags = IOParser.ParseRequest(_baseAPIGatewayProxyRequest);

            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("method"), Is.True);
                Assert.That(tags.Keys.Contains("uri"), Is.True);
                Assert.That(tags.Keys.Contains("headers.x-forwarded-proto"), Is.True);
                Assert.That(tags.Keys.Contains("headers.x-forwarded-port"), Is.True);
                Assert.That(tags.Keys.Contains("headers.content-type"), Is.True);
                Assert.That(tags.Keys.Contains("headers.content-length"), Is.True);
                Assert.That(tags.Keys.Contains("newrelic"), Is.True);

                Assert.That(tags["method"], Is.EqualTo("POST"));
                Assert.That(tags["uri"], Is.EqualTo("/test/path"));
                Assert.That(tags["headers.x-forwarded-proto"], Is.EqualTo("proto"));
                Assert.That(tags["headers.x-forwarded-port"], Is.EqualTo("1234"));
                Assert.That(tags["headers.content-type"], Is.EqualTo("application/json"));
                Assert.That(tags["headers.content-length"], Is.EqualTo("1000"));
                Assert.That(tags["newrelic"], Is.EqualTo("dt-payload"));

                Assert.That(tags.Keys.Contains("headers.access-control-allow-origin"), Is.False);
                Assert.That(tags.Keys.Contains("headers.custom-header"), Is.False);
            });

        }

        [Test]
        public void ParseRequest_APIGatewayProxyRequest_WithMultiValueHeaders()
        {
            _baseAPIGatewayProxyRequest.MultiValueHeaders = _multiValueHeaders;
            var tags = IOParser.ParseRequest(_baseAPIGatewayProxyRequest);

            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("method"), Is.True);
                Assert.That(tags.Keys.Contains("uri"), Is.True);
                Assert.That(tags.Keys.Contains("headers.x-forwarded-proto"), Is.True);
                Assert.That(tags.Keys.Contains("headers.x-forwarded-port"), Is.True);
                Assert.That(tags.Keys.Contains("headers.content-type"), Is.True);
                Assert.That(tags.Keys.Contains("headers.content-length"), Is.True);
                Assert.That(tags.Keys.Contains("newrelic"), Is.True);

                Assert.That(tags["method"], Is.EqualTo("POST"));
                Assert.That(tags["uri"], Is.EqualTo("/test/path"));
                Assert.That(tags["headers.x-forwarded-proto"], Is.EqualTo("proto"));
                Assert.That(tags["headers.x-forwarded-port"], Is.EqualTo("1234,4321"));
                Assert.That(tags["headers.content-type"], Is.EqualTo("application/json"));
                Assert.That(tags["headers.content-length"], Is.EqualTo("1000"));
                Assert.That(tags["newrelic"], Is.EqualTo("dt-payload"));

                Assert.That(tags.Keys.Contains("headers.access-control-allow-origin"), Is.False);
                Assert.That(tags.Keys.Contains("headers.custom-header"), Is.False);
            });
        }

        [Test]
        public void ParseResponse_APIGatewayProxyResponse_WithoutHeaders()
        {
            var tags = IOParser.ParseResponse(_baseAPIGatewayProxyResponse);

            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("status"), Is.True);

                Assert.That(tags["status"], Is.EqualTo("200"));
            });
        }

        [Test]
        public void ParseResponse_APIGatewayProxyResponse_WithSingleValueHeaders()
        {
            _baseAPIGatewayProxyResponse.Headers = _singleValueHeaders;
            var tags = IOParser.ParseResponse(_baseAPIGatewayProxyResponse);

            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("status"), Is.True);
                Assert.That(tags.Keys.Contains("headers.x-forwarded-proto"), Is.True);
                Assert.That(tags.Keys.Contains("headers.x-forwarded-port"), Is.True);
                Assert.That(tags.Keys.Contains("headers.content-type"), Is.True);
                Assert.That(tags.Keys.Contains("headers.content-length"), Is.True);
                Assert.That(tags.Keys.Contains("newrelic"), Is.True);

                Assert.That(tags["status"], Is.EqualTo("200"));
                Assert.That(tags["headers.x-forwarded-proto"], Is.EqualTo("proto"));
                Assert.That(tags["headers.x-forwarded-port"], Is.EqualTo("1234"));
                Assert.That(tags["headers.content-type"], Is.EqualTo("application/json"));
                Assert.That(tags["headers.content-length"], Is.EqualTo("1000"));
                Assert.That(tags["newrelic"], Is.EqualTo("dt-payload"));

                Assert.That(tags.Keys.Contains("headers.access-control-allow-origin"), Is.False);
                Assert.That(tags.Keys.Contains("headers.custom-header"), Is.False);
            });
        }

        [Test]
        public void ParseResponse_APIGatewayProxyResponse_WithMultiValueHeaders()
        {
            _baseAPIGatewayProxyResponse.MultiValueHeaders = _multiValueHeaders;
            var tags = IOParser.ParseResponse(_baseAPIGatewayProxyResponse);

            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("status"), Is.True);
                Assert.That(tags.Keys.Contains("headers.x-forwarded-proto"), Is.True);
                Assert.That(tags.Keys.Contains("headers.x-forwarded-port"), Is.True);
                Assert.That(tags.Keys.Contains("headers.content-type"), Is.True);
                Assert.That(tags.Keys.Contains("headers.content-length"), Is.True);
                Assert.That(tags.Keys.Contains("newrelic"), Is.True);

                Assert.That(tags["status"], Is.EqualTo("200"));
                Assert.That(tags["headers.x-forwarded-proto"], Is.EqualTo("proto"));
                Assert.That(tags["headers.x-forwarded-port"], Is.EqualTo("1234,4321"));
                Assert.That(tags["headers.content-type"], Is.EqualTo("application/json"));
                Assert.That(tags["headers.content-length"], Is.EqualTo("1000"));
                Assert.That(tags["newrelic"], Is.EqualTo("dt-payload"));

                Assert.That(tags.Keys.Contains("headers.access-control-allow-origin"), Is.False);
                Assert.That(tags.Keys.Contains("headers.custom-header"), Is.False);
            });
        }

        #endregion

        #region ApplicationLoadBalancer

        [Test]
        public void ParseRequest_ApplicationLoadBalancerRequest_InvocationSourceNull()
        {
            _baseApplicationLoadBalancerRequest.RequestContext = null;
            var tags = IOParser.ParseRequest(_baseApplicationLoadBalancerRequest);
            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("method"), Is.True);
                Assert.That(tags.Keys.Contains("uri"), Is.True);
            });

            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("method"), Is.True);
                Assert.That(tags.Keys.Contains("uri"), Is.True);
                Assert.That(tags.Keys.Contains("aws.lambda.eventSource.arn"), Is.False);
                Assert.That(tags["method"], Is.EqualTo("POST"));
                Assert.That(tags["uri"], Is.EqualTo("/test/path"));
            });
        }

        [Test]
        public void ParseRequest_ApplicationLoadBalancerRequest_WithoutHeaders()
        {
            var tags = IOParser.ParseRequest(_baseApplicationLoadBalancerRequest);
            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("method"), Is.True);
                Assert.That(tags.Keys.Contains("uri"), Is.True);
            });

            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("method"), Is.True);
                Assert.That(tags.Keys.Contains("uri"), Is.True);
                Assert.That(tags.Keys.Contains("aws.lambda.eventSource.arn"), Is.True);
                Assert.That(tags["method"], Is.EqualTo("POST"));
                Assert.That(tags["uri"], Is.EqualTo("/test/path"));
                Assert.That(tags["aws.lambda.eventSource.arn"], Is.EqualTo("test-arn"));
            });
        }

        [Test]
        public void ParseRequest_ApplicationLoadBalancerRequest_WithSingleValueHeaders()
        {
            _baseApplicationLoadBalancerRequest.Headers = _singleValueHeaders;
            var tags = IOParser.ParseRequest(_baseApplicationLoadBalancerRequest);
            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("method"), Is.True);
                Assert.That(tags.Keys.Contains("uri"), Is.True);
                Assert.That(tags.Keys.Contains("aws.lambda.eventSource.arn"), Is.True);
                Assert.That(tags.Keys.Contains("headers.x-forwarded-proto"), Is.True);
                Assert.That(tags.Keys.Contains("headers.x-forwarded-port"), Is.True);
                Assert.That(tags.Keys.Contains("headers.content-type"), Is.True);
                Assert.That(tags.Keys.Contains("headers.content-length"), Is.True);
                Assert.That(tags.Keys.Contains("newrelic"), Is.True);

                Assert.That(tags["method"], Is.EqualTo("POST"));
                Assert.That(tags["uri"], Is.EqualTo("/test/path"));
                Assert.That(tags["aws.lambda.eventSource.arn"], Is.EqualTo("test-arn"));
                Assert.That(tags["headers.x-forwarded-proto"], Is.EqualTo("proto"));
                Assert.That(tags["headers.x-forwarded-port"], Is.EqualTo("1234"));
                Assert.That(tags["headers.content-type"], Is.EqualTo("application/json"));
                Assert.That(tags["headers.content-length"], Is.EqualTo("1000"));
                Assert.That(tags["newrelic"], Is.EqualTo("dt-payload"));

                Assert.That(tags.Keys.Contains("headers.access-control-allow-origin"), Is.False);
                Assert.That(tags.Keys.Contains("headers.custom-header"), Is.False);
            });
        }

        [Test]
        public void ParseRequest_ApplicationLoadBalancerRequest_WithMultiValueHeaders()
        {
            _baseApplicationLoadBalancerRequest.MultiValueHeaders = _multiValueHeaders;
            var tags = IOParser.ParseRequest(_baseApplicationLoadBalancerRequest);

            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("method"), Is.True);
                Assert.That(tags.Keys.Contains("uri"), Is.True);
                Assert.That(tags.Keys.Contains("aws.lambda.eventSource.arn"), Is.True);
                Assert.That(tags.Keys.Contains("headers.x-forwarded-proto"), Is.True);
                Assert.That(tags.Keys.Contains("headers.x-forwarded-port"), Is.True);
                Assert.That(tags.Keys.Contains("headers.content-type"), Is.True);
                Assert.That(tags.Keys.Contains("headers.content-length"), Is.True);
                Assert.That(tags.Keys.Contains("newrelic"), Is.True);

                Assert.That(tags["method"], Is.EqualTo("POST"));
                Assert.That(tags["uri"], Is.EqualTo("/test/path"));
                Assert.That(tags["aws.lambda.eventSource.arn"], Is.EqualTo("test-arn"));
                Assert.That(tags["headers.x-forwarded-proto"], Is.EqualTo("proto"));
                Assert.That(tags["headers.x-forwarded-port"], Is.EqualTo("1234,4321"));
                Assert.That(tags["headers.content-type"], Is.EqualTo("application/json"));
                Assert.That(tags["headers.content-length"], Is.EqualTo("1000"));
                Assert.That(tags["newrelic"], Is.EqualTo("dt-payload"));

                Assert.That(tags.Keys.Contains("headers.access-control-allow-origin"), Is.False);
                Assert.That(tags.Keys.Contains("headers.custom-header"), Is.False);
            });
        }

        [Test]
        public void ParseResponse_ApplicationLoadBalancerResponse_WithoutHeaders()
        {
            var tags = IOParser.ParseResponse(_baseApplicationLoadBalancerResponse);
            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("status"), Is.True);

                Assert.That(tags["status"], Is.EqualTo("200"));
            });
        }

        [Test]
        public void ParseResponse_ApplicationLoadBalancerResponse_WithSingleValueHeaders()
        {
            _baseApplicationLoadBalancerResponse.Headers = _singleValueHeaders;
            var tags = IOParser.ParseResponse(_baseApplicationLoadBalancerResponse);

            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("status"), Is.True);
                Assert.That(tags.Keys.Contains("headers.x-forwarded-proto"), Is.True);
                Assert.That(tags.Keys.Contains("headers.x-forwarded-port"), Is.True);
                Assert.That(tags.Keys.Contains("headers.content-type"), Is.True);
                Assert.That(tags.Keys.Contains("headers.content-length"), Is.True);
                Assert.That(tags.Keys.Contains("newrelic"), Is.True);

                Assert.That(tags["status"], Is.EqualTo("200"));
                Assert.That(tags["headers.x-forwarded-proto"], Is.EqualTo("proto"));
                Assert.That(tags["headers.x-forwarded-port"], Is.EqualTo("1234"));
                Assert.That(tags["headers.content-type"], Is.EqualTo("application/json"));
                Assert.That(tags["headers.content-length"], Is.EqualTo("1000"));
                Assert.That(tags["newrelic"], Is.EqualTo("dt-payload"));

                Assert.That(tags.Keys.Contains("headers.access-control-allow-origin"), Is.False);
                Assert.That(tags.Keys.Contains("headers.custom-header"), Is.False);
            });
            ;
        }

        [Test]
        public void ParseResponse_ApplicationLoadBalancerResponse_WithMultiValueHeaders()
        {
            _baseApplicationLoadBalancerResponse.MultiValueHeaders = _multiValueHeaders;
            var tags = IOParser.ParseResponse(_baseApplicationLoadBalancerResponse);

            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("status"), Is.True);
                Assert.That(tags.Keys.Contains("headers.x-forwarded-proto"), Is.True);
                Assert.That(tags.Keys.Contains("headers.x-forwarded-port"), Is.True);
                Assert.That(tags.Keys.Contains("headers.content-type"), Is.True);
                Assert.That(tags.Keys.Contains("headers.content-length"), Is.True);
                Assert.That(tags.Keys.Contains("newrelic"), Is.True);

                Assert.That(tags["status"], Is.EqualTo("200"));
                Assert.That(tags["headers.x-forwarded-proto"], Is.EqualTo("proto"));
                Assert.That(tags["headers.x-forwarded-port"], Is.EqualTo("1234,4321"));
                Assert.That(tags["headers.content-type"], Is.EqualTo("application/json"));
                Assert.That(tags["headers.content-length"], Is.EqualTo("1000"));
                Assert.That(tags["newrelic"], Is.EqualTo("dt-payload"));

                Assert.That(tags.Keys.Contains("headers.access-control-allow-origin"), Is.False);
                Assert.That(tags.Keys.Contains("headers.custom-header"), Is.False);
            });
        }

        #endregion

        #region SQSEvent

        [Test]
        public void ParseRequest_SQSEvent_InvocationSourceNotNull_Tags()
        {
            var tags = IOParser.ParseRequest(_baseSQSEvent);

            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("aws.lambda.eventSource.arn"), Is.True);
                Assert.That(tags["aws.lambda.eventSource.arn"], Is.EqualTo("test-arn"));
            });
        }

        [Test]
        public void ParseRequest_SQSEvent_InvocationSourceNull_Tags()
        {
            _baseSQSEvent.Records = null;
            var tags = IOParser.ParseRequest(_baseSQSEvent);

            Assert.That(tags.Keys.Contains("aws.lambda.eventSource.arn"), Is.False);
        }

        #endregion

        #region SNSEvent

        [Test]
        public void ParseRequest_SNSEvent_InvocationSourceNotNull_Tags()
        {
            var tags = IOParser.ParseRequest(_baseSNSEvent);

            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("aws.lambda.eventSource.arn"), Is.True);
                Assert.That(tags["aws.lambda.eventSource.arn"], Is.EqualTo("test-arn"));
            });
        }

        [Test]
        public void ParseRequest_SNSEvent_InvocationSourceNull_Tags()
        {
            _baseSNSEvent.Records = null;
            var tags = IOParser.ParseRequest(_baseSNSEvent);

            Assert.That(tags.Keys.Contains("aws.lambda.eventSource.arn"), Is.False);
        }

        #endregion

        #region KinesisEvent

        [Test]
        public void ParseRequest_KinesisEvent_InvocationSourceNotNull_Tags()
        {
            var tags = IOParser.ParseRequest(_baseKinesisEvent);

            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("aws.lambda.eventSource.arn"), Is.True);
                Assert.That(tags["aws.lambda.eventSource.arn"], Is.EqualTo("test-arn"));
            });
        }

        [Test]
        public void ParseRequest_KinesisEvent_InvocationSourceNull_Tags()
        {
            _baseKinesisEvent.Records = null;
            var tags = IOParser.ParseRequest(_baseKinesisEvent);

            Assert.That(tags.Keys.Contains("aws.lambda.eventSource.arn"), Is.False);
        }

        #endregion

        #region S3Event

        [Test]
        public void ParseRequest_S3Event_InvocationSourceNotNull_Tags()
        {
            var tags = IOParser.ParseRequest(_baseS3Event);

            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("aws.lambda.eventSource.arn"), Is.True);
                Assert.That(tags["aws.lambda.eventSource.arn"], Is.EqualTo("test-arn"));
            });
        }

        [Test]
        public void ParseRequest_S3Event_InvocationSourceNull_Tags()
        {
            _baseS3Event.Records = null;
            var tags = IOParser.ParseRequest(_baseS3Event);

            Assert.That(tags.Keys.Contains("aws.lambda.eventSource.arn"), Is.False);
        }

        #endregion

        #region DynamoDBEvent

        [Test]
        public void ParseRequest_DynamoDBEvent_InvocationSourceNotNull_Tags()
        {
            var tags = IOParser.ParseRequest(_baseDynamoDBEvent);

            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("aws.lambda.eventSource.arn"), Is.True);
                Assert.That(tags["aws.lambda.eventSource.arn"], Is.EqualTo("test-arn"));
            });
        }

        [Test]
        public void ParseRequest_DynamoDBEvent_InvocationSourceNull_Tags()
        {
            _baseDynamoDBEvent.Records = null;
            var tags = IOParser.ParseRequest(_baseDynamoDBEvent);

            Assert.That(tags.Keys.Contains("aws.lambda.eventSource.arn"), Is.False);
        }

        #endregion

        #region KinesisFirehoseEvent

        [Test]
        public void ParseRequest_KinesisFirehoseEvent_InvocationSourceNotNull_Tags()
        {
            var tags = IOParser.ParseRequest(_baseKinesisFirehoseEvent);

            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("aws.lambda.eventSource.arn"), Is.True);
                Assert.That(tags["aws.lambda.eventSource.arn"], Is.EqualTo("test-arn"));
            });
        }

        [Test]
        public void ParseRequest_KinesisFirehoseEvent_InvocationSourceNull_Tags()
        {
            _baseKinesisFirehoseEvent.DeliveryStreamArn = null;
            var tags = IOParser.ParseRequest(_baseKinesisFirehoseEvent);

            Assert.That(tags.Keys.Contains("aws.lambda.eventSource.arn"), Is.False);
        }

        #endregion

        #region IDictionary

        [Test]
        public void ParseResponse_IDictionary_WithoutStatus()
        {
            var response = new Dictionary<string, object>
            {
                { "CustomItem", "CustomValue" }
            };
            var tags = IOParser.ParseResponse(response);
            Assert.That(tags.Keys.Contains("status"), Is.False);
        }

        [Test]
        public void ParseResponse_IDictionary_WithStatusAsString()
        {
            var response = new Dictionary<string, object>
            {
                { "statusCode", "200" }
            };
            var tags = IOParser.ParseResponse(response);
            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("status"), Is.True);

                Assert.That(tags["status"], Is.EqualTo("200"));
            });
        }

        [Test]
        public void ParseResponse_IDictionary_WithStatusAsInt()
        {
            var response = new Dictionary<string, object>
            {
                { "statusCode", 200 }
            };
            var tags = IOParser.ParseResponse(response);
            Assert.Multiple(() =>
            {
                Assert.That(tags.Keys.Contains("status"), Is.True);

                Assert.That(tags["status"], Is.EqualTo("200"));
            });
        }

        #endregion

    }
}
