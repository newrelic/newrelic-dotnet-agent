/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;

namespace NewRelic.Core.Logging
{
    public class NoOpLogger : ILogger
    {
        public bool IsDebugEnabled => false;

        public bool IsErrorEnabled => false;

        public bool IsFinestEnabled => false;

        public bool IsInfoEnabled => false;

        public bool IsWarnEnabled => false;

        public void Debug(Exception exception)
        {

        }

        public void Debug(string message)
        {

        }

        public void DebugFormat(string format, params object[] args)
        {

        }

        public void Error(Exception exception)
        {

        }

        public void Error(string message)
        {

        }

        public void ErrorFormat(string format, params object[] args)
        {

        }

        public void Finest(Exception exception)
        {

        }

        public void Finest(string message)
        {

        }

        public void FinestFormat(string format, params object[] args)
        {

        }

        public void Info(Exception exception)
        {

        }

        public void Info(string message)
        {

        }

        public void InfoFormat(string format, params object[] args)
        {

        }

        public void Warn(Exception exception)
        {

        }

        public void Warn(string message)
        {

        }

        public void WarnFormat(string format, params object[] args)
        {

        }
    }
}
