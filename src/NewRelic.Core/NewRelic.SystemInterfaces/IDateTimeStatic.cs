/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;

namespace NewRelic.SystemInterfaces
{
    public interface IDateTimeStatic
    {
        DateTime Now { get; }

        DateTime UtcNow { get; }
    }

    public class DateTimeStatic : IDateTimeStatic
    {
        public DateTime Now { get { return DateTime.Now; } }

        public DateTime UtcNow { get { return DateTime.UtcNow; } }
    }
}
