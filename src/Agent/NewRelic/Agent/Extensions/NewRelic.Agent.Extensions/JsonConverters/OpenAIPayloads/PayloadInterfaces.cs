// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Extensions.JsonConverters.OpenAIPayloads
{
    public interface IRequestPayload
    {
        MessageObj[] Messages { get; set; }

        string Model { get; set; }
    }

    public interface IResponsePayload
    {
        string Id { get; set; }

        string Object { get; set; }

        string Created { get; set; }

        string Model { get; set; }

        ChoicesObj[] Choices { get; set; }

        string SystemFingerprint { get; set; }
    }

    public class ResponseData
    {
        public string Text { get; set; }
    }

    public class ResponseUsage
    {
        public int OutputTokenCount { get; set; }

        public int InputTokenCount { get; set; }

        public int TotalTokenCount { get; set; }
    }
}
