// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using NewRelic.Agent.Helpers;

namespace NewRelic.Agent.Core.Utilities
{
    public static class StackTraces
    {
        public static ICollection<string> ScrubAndTruncate(Exception exception, int maxDepth)
        {
            if (null == exception)
            {
                return new List<string>(0);
            }

            var frames = new List<string>(maxDepth);
            var exceptions = new List<Exception>(5);
            while (exception != null)
            {
                exceptions.Add(exception);
                exception = exception.InnerException;
            }

            if (exceptions.Count == 1)
            {
                return ScrubAndTruncate(exceptions[0].StackTrace, maxDepth);
            }

            exceptions.Reverse();
            foreach (var ex in exceptions)
            {
                frames.Add(string.Format("[{0}: {1}]", ex.GetType().Name, ex.Message));
                var exFrames = ScrubAndTruncate(ex.StackTrace, maxDepth - frames.Count);
                frames.AddRange(exFrames);
                if (frames.Count > maxDepth)
                {
                    return frames;
                }

                frames.Add(" ");
            }

            return frames;
        }

        public static string MethodToString(MethodBase method)
        {
            return string.Format("{0}.{1}({2})", method.DeclaringType.FullName, method.Name, FormatMethodParameters(method.GetParameters()));
        }

        private static string FormatMethodParameters(ParameterInfo[] parameterInfo)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < parameterInfo.Length; i++)
            {
                var info = parameterInfo[i];
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(info.ParameterType.FullName).Append(' ').Append(info.Name);
            }

            return builder.ToString();
        }

        public static IList<string> ScrubAndTruncate(string stackTrace, int maxDepth)
        {
            var stackTraces = ParseStackTrace(stackTrace);
            var list = new List<string>(stackTraces.Length);
            foreach (var line in stackTraces)
            {
                if (line != null && line.IndexOf("at NewRelic.Agent", 0, Math.Min(20, line.Length)) < 0)
                {
                    if (list.Count >= maxDepth)
                    {
                        return list;
                    }

                    list.Add('\t' + line);
                }
            }

            return list;
        }

        public static string[] ParseStackTrace(string stackTrace)
        {
            if (null == stackTrace)
            {
                return new string[0];
            }

            return stackTrace.Split(StringSeparators.StringNewLine, StringSplitOptions.None);
        }

        public static IList<StackFrame> ScrubAndTruncate(StackTrace stackTrace, int maxDepth)
        {
            return ScrubAndTruncate(stackTrace.GetFrames(), maxDepth);
        }

        public static IList<StackFrame> ScrubAndTruncate(StackFrame[] frames, int maxDepth)
        {
            var maxFrames = Math.Min(frames.Length, maxDepth);
            var stackFrames = new List<StackFrame>(maxFrames);
            for (var i = 0; i < frames.Length && stackFrames.Count < maxDepth; i++)
            {
                if (frames[i].GetMethod().DeclaringType != null && !frames[i].GetMethod().DeclaringType.FullName.StartsWith("NewRelic"))
                {
                    stackFrames.Add(frames[i]);
                }
            }

            return stackFrames;
        }

        public static string ToString(StackFrame frame)
        {
            var method = frame.GetMethod();
            var typeName = method.DeclaringType == null ? "null" : method.DeclaringType.FullName;
            return string.Format("{0}.{1}({2}:{3})", typeName, method.Name, frame.GetFileName(), frame.GetFileLineNumber());
        }

        public static ICollection<string> ToStringList(IList<StackFrame> stackFrames)
        {
            var stringList = new List<string>(stackFrames.Count);
            for (var i = 0; i < stackFrames.Count; i++)
            {
                // the stackFrames can have empty spots
                if (stackFrames[i] == null)
                {
                    continue;
                }

                stringList.Add(ToString(stackFrames[i]));
            }

            return stringList;
        }

        public static object ToJson(Exception ex)
        {
            var exception = new Dictionary<string, string>();
            exception.Add("message", ex.Message);
            exception.Add("backtrace", ex.StackTrace);

            var dict = new Dictionary<string, object>();
            dict.Add("exception", exception);
            return dict;
        }

    }
}

