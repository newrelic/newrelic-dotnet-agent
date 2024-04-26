// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda
{
    public abstract class LambdaSesEventTriggerFixtureBase : LambdaSelfExecutingAssemblyFixture
    {
        protected LambdaSesEventTriggerFixtureBase(string targetFramework, bool isAsync) :
            base(targetFramework,
                null,
                "LambdaSelfExecutingAssembly::LambdaSelfExecutingAssembly.Program::SesEventHandler" + (isAsync ? "Async" : ""),
                "SesEvent" + (isAsync ? "Async" : ""),
                null)
        {
        }

        public void EnqueueEvent()
        {
            var eventJson = """
                            {
                              "Records": [
                                {
                                  "eventSource": "aws:ses",
                                  "eventVersion": "1.0",
                                  "ses": {
                                    "mail": {
                                      "commonHeaders": {
                                        "date": "Wed, 7 Oct 2015 12:34:56 -0700",
                                        "from": [
                                          "Jane Doe <janedoe@example.com>"
                                        ],
                                        "messageId": "<0123456789example.com>",
                                        "returnPath": "janedoe@example.com",
                                        "subject": "Test Subject",
                                        "to": [
                                          "johndoe@example.com"
                                        ]
                                      },
                                      "destination": [
                                        "johndoe@example.com"
                                      ],
                                      "headers": [
                                        {
                                          "name": "Return-Path",
                                          "value": "<janedoe@example.com>"
                                        },
                                        {
                                          "name": "Received",
                                          "value": "from mailer.example.com (mailer.example.com [203.0.113.1]) by inbound-smtp.{dns_suffix} with SMTP id o3vrnil0e2ic28trm7dfhrc2v0cnbeccl4nbp0g1 for johndoe@example.com; Wed, 07 Oct 2015 12:34:56 +0000 (UTC)"
                                        },
                                        {
                                          "name": "DKIM-Signature",
                                          "value": "v=1; a=rsa-sha256; c=relaxed/relaxed; d=example.com; s=example; h=mime-version:from:date:message-id:subject:to:content-type; bh=jX3F0bCAI7sIbkHyy3mLYO28ieDQz2R0P8HwQkklFj4=; b=sQwJ+LMe9RjkesGu+vqU56asvMhrLRRYrWCbVt6WJulueecwfEwRf9JVWgkBTKiL6m2hr70xDbPWDhtLdLO+jB3hzjVnXwK3pYIOHw3vxG6NtJ6o61XSUwjEsp9tdyxQjZf2HNYee873832l3K1EeSXKzxYk9Pwqcpi3dMC74ct9GukjIevf1H46hm1L2d9VYTL0LGZGHOAyMnHmEGB8ZExWbI+k6khpurTQQ4sp4PZPRlgHtnj3Zzv7nmpTo7dtPG5z5S9J+L+Ba7dixT0jn3HuhaJ9b+VThboo4YfsX9PMNhWWxGjVksSFOcGluPO7QutCPyoY4gbxtwkN9W69HA=="
                                        },
                                        {
                                          "name": "MIME-Version",
                                          "value": "1.0"
                                        },
                                        {
                                          "name": "From",
                                          "value": "Jane Doe <janedoe@example.com>"
                                        },
                                        {
                                          "name": "Date",
                                          "value": "Wed, 7 Oct 2015 12:34:56 -0700"
                                        },
                                        {
                                          "name": "Message-ID",
                                          "value": "<0123456789example.com>"
                                        },
                                        {
                                          "name": "Subject",
                                          "value": "Test Subject"
                                        },
                                        {
                                          "name": "To",
                                          "value": "johndoe@example.com"
                                        },
                                        {
                                          "name": "Content-Type",
                                          "value": "text/plain; charset=UTF-8"
                                        }
                                      ],
                                      "headersTruncated": false,
                                      "messageId": "o3vrnil0e2ic28trm7dfhrc2v0clambda4nbp0g1",
                                      "source": "janedoe@example.com",
                                      "timestamp": "1970-01-01T00:00:00Z"
                                    },
                                    "receipt": {
                                      "action": {
                                        "functionArn": "arn:{partition}:lambda:{region}:012345678912:function:Example",
                                        "invocationType": "Event",
                                        "type": "Lambda"
                                      },
                                      "dkimVerdict": {
                                        "status": "PASS"
                                      },
                                      "processingTimeMillis": 574,
                                      "recipients": [
                                        "johndoe@example.com"
                                      ],
                                      "spamVerdict": {
                                        "status": "PASS"
                                      },
                                      "spfVerdict": {
                                        "status": "PASS"
                                      },
                                      "timestamp": "1970-01-01T00:00:00Z",
                                      "virusVerdict": {
                                        "status": "PASS"
                                      }
                                    }
                                  }
                                }
                              ]
                            }
                            """;
            EnqueueLambdaEvent(eventJson);
        }
    }

    public class LambdaSesEventTriggerFixtureNet6 : LambdaSesEventTriggerFixtureBase
    {
        public LambdaSesEventTriggerFixtureNet6() : base("net6.0", false) { }
    }

    public class AsyncLambdaSesEventTriggerFixtureNet6 : LambdaSesEventTriggerFixtureBase
    {
        public AsyncLambdaSesEventTriggerFixtureNet6() : base("net6.0", true) { }
    }

    public class LambdaSesEventTriggerFixtureNet8 : LambdaSesEventTriggerFixtureBase
    {
        public LambdaSesEventTriggerFixtureNet8() : base("net8.0", false) { }
    }

    public class AsyncLambdaSesEventTriggerFixtureNet8 : LambdaSesEventTriggerFixtureBase
    {
        public AsyncLambdaSesEventTriggerFixtureNet8() : base("net8.0", true) { }
    }
}
