// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NewRelic.Testing.Assertions
{
    public class NrAssert
    {
        public static T Throws<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T ex)
            {
                return ex;
            }
            catch (Exception ex)
            {
                throw new TestFailureException(
                    $"Expected exception of type '{typeof(T)}', but exception of type '{ex.GetType()}' was thrown instead: {Environment.NewLine}{FormatException(ex)}");
            }

            throw new TestFailureException($"Expected exception of type '{typeof(T)}' was not thrown.");
        }

        public static void Multiple(params Action[] actions)
        {
            var exceptions = new List<Tuple<int,Exception>>();
            for(var i = 0; i < actions.Length; i++)
            {
                try
                {
                    actions[i]();
                }
                catch (TestFailureException testEx)
                {
                    exceptions.Add(new Tuple<int, Exception>(i, testEx));
                }
                catch (Exception ex)
                {
                    exceptions.Add(new Tuple<int, Exception>(i, ex));
                }
            }

            if (!exceptions.Any())
                return;

            var details = FormatExceptions(exceptions);
            throw new TestFailureException(details);
        }

        private static string FormatException(Exception exception)
        {
            var exceptionName = exception.GetType().Name;
            var failureType = exceptionName.Contains("Assertion") ? "Assertion failed" : exception.GetType().Name;
            var stackTrace = new StackTrace(exception, true);
            var maybeLineNumber = stackTrace.GetFrames()?.FirstOrDefault(frame => frame?.GetFileLineNumber() > 0)?.GetFileLineNumber();
            var lineNumberOrUnknown = maybeLineNumber?.ToString() ?? "[unknown]";

            return $"{failureType} on line {lineNumberOrUnknown}: {exception.Message}";
        }

        private static string FormatExceptions(IEnumerable<Tuple<int, Exception>> exceptions)
        {
            var strings = new List<string>();

            foreach (var exceptionTuple in exceptions)
            {
                if (exceptionTuple == null || exceptionTuple.Item2 == null)
                    continue;

                var exceptionName = exceptionTuple.Item2.GetType().Name;
                var failureType = exceptionName.Contains("Assertion") ? "Assertion failed" : exceptionTuple.Item2.GetType().Name;
                var stackTrace = new StackTrace(exceptionTuple.Item2, true);
                var maybeLineNumber = stackTrace.GetFrames()?.FirstOrDefault(frame => frame?.GetFileLineNumber() > 0)?.GetFileLineNumber();
                var lineNumberOrUnknown = maybeLineNumber?.ToString() ?? "[unknown]";

                strings.Add($"Assertion #{exceptionTuple.Item1+1}: {FormatException(exceptionTuple.Item2)}");
            }

            return Environment.NewLine + string.Join(Environment.NewLine + Environment.NewLine, strings.ToArray());
        }
    }

    public class TestFailureException : Exception
    {
        public TestFailureException(string message) : base(message) { }
    }
}
