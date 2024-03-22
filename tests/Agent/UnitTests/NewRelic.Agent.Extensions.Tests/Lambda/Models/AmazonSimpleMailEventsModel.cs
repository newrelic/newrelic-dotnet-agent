// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Mock.Amazon.Lambda.SimpleEmailEvents
{
    public class SimpleEmailEvent
    {
        public List<SimpleEmailEventRecord> Records { get; set; }
    }

    public class SimpleEmailEventRecord
    {
        public SesEntity Ses { get; set; }
    }

    public class SesEntity
    {
        public SimpleEmailEntity Mail { get; set; }
    }

    public class SimpleEmailEntity
    {
        public SimpleEmailCommonHeadersEntity CommonHeaders { get; set; }
    }   
    public class SimpleEmailCommonHeadersEntity
    {
        public string MessageId { get; set; }
        public string Date { get; set; }
        public string ReturnPath { get; set; }
    }
}
