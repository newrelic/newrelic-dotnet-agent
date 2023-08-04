// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace ReleaseNotesBuilder
{
    public readonly struct Entry
    {
        public Entry(string front, string body)
        {
            Body = body;
            Front = front;
        }

        public string Body { get; }
        public string Front { get; }
    }
}
