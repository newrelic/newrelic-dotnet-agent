// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NewRelic.Api.Agent;

namespace AspNetCore3Features.Controllers
{
    public class InterfaceDefaultsController : Controller
    {
        [HttpGet]
        public string GetWithAttributes()
        {
            ILoggerWithAttributes consoleLoggerWithAttributesNoDefault = new ConsoleLoggerWithAttributesNoDefault();
            consoleLoggerWithAttributesNoDefault.LogException(new Exception("My logged exception"));

            ILoggerWithAttributes consoleLoggerWithAttributesOverridesDefault = new ConsoleLoggerWithAttributesOverridesDefault();
            consoleLoggerWithAttributesOverridesDefault.LogException(new Exception("My logged exception"));

            return "Done";
        }

        [HttpGet]
        public string GetWithoutAttributes()
        {
            ILoggerNoAttributes consoleLoggerNoAttributesNoDefault = new ConsoleLoggerNoAttributesNoDefault();
            consoleLoggerNoAttributesNoDefault.LogException(new Exception("My logged exception"));

            ILoggerNoAttributes consoleLoggerNoAttributesOverridesDefault = new ConsoleLoggerNoAttributesOverridesDefault();
            consoleLoggerNoAttributesOverridesDefault.LogException(new Exception("My logged exception"));

            return "Done";
        }
    }

    public interface ILoggerWithAttributes
    {
        void LogMessage(LogLevel level, string message);

        [Trace]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        void LogException(Exception ex) => LogMessage(LogLevel.Error, ex.ToString());
    }

    public class ConsoleLoggerWithAttributesNoDefault : ILoggerWithAttributes
    {
        [Trace]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void LogMessage(LogLevel level, string message) { System.Diagnostics.Debug.WriteLine("{0} - {1}", level, message); }
    }

    public class ConsoleLoggerWithAttributesOverridesDefault : ILoggerWithAttributes
    {
        [Trace]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void LogMessage(LogLevel level, string message) { System.Diagnostics.Debug.WriteLine("{0} - {1}", level, message); }

        [Trace]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void LogException(Exception ex) => LogMessage(LogLevel.Error, "Logging: " + ex.ToString());
    }

    public interface ILoggerNoAttributes
    {
        void LogMessage(LogLevel level, string message);

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        void LogException(Exception ex) => LogMessage(LogLevel.Error, ex.ToString());
    }

    public class ConsoleLoggerNoAttributesNoDefault : ILoggerNoAttributes
    {
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void LogMessage(LogLevel level, string message) { System.Diagnostics.Debug.WriteLine("{0} - {1}", level, message); }
    }

    public class ConsoleLoggerNoAttributesOverridesDefault : ILoggerNoAttributes
    {
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void LogMessage(LogLevel level, string message) { System.Diagnostics.Debug.WriteLine("{0} - {1}", level, message); }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void LogException(Exception ex) => LogMessage(LogLevel.Error, "Logging: " + ex.ToString());
    }
}
