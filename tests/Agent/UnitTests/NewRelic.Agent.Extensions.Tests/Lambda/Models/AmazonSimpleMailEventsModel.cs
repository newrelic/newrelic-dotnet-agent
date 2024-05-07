// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Mock.Amazon.Lambda.SimpleEmailEvents
{
    // https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.SimpleEmailEvents/SimpleEmailEvent.cs
    public class SimpleEmailEvent<TReceiptAction> where TReceiptAction : IReceiptAction
    {
        public List<SimpleEmailRecord<TReceiptAction>> Records { get; set; }

        public class SimpleEmailRecord<TReceiptAction1> where TReceiptAction1 : IReceiptAction
        {
            public SimpleEmailService<TReceiptAction1> Ses { get; set; }
        }

        public class SimpleEmailService<TReceiptAction2> where TReceiptAction2 : IReceiptAction
        {
            public SimpleEmailMessage Mail { get; set; }
        }

        public class SimpleEmailMessage
        {
            public SimpleEmailCommonHeaders CommonHeaders { get; set; }
        }
        public class SimpleEmailCommonHeaders
        {
            public string MessageId { get; set; }
            public string Date { get; set; } // yes, it's really a string
            public string ReturnPath { get; set; }
        }
    }

    // https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.SimpleEmailEvents/Actions/IReceiptAction.cs
    public interface IReceiptAction
    {
        string Type { get; set; }
    }
    public class MockReceiptAction : IReceiptAction
    {
        public string Type { get; set; }
    }
}
