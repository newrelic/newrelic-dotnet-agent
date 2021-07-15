// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using NewRelic.Agent.Helpers;

namespace NewRelic.Agent.Core.Utils
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
            foreach (Exception ex in exceptions)
            {
                frames.Add(string.Format("[{0}: {1}]", ex.GetType().Name, ex.Message));
                ICollection<string> exFrames = ScrubAndTruncate(ex.StackTrace, maxDepth - frames.Count);
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
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < parameterInfo.Length; i++)
            {
                ParameterInfo info = parameterInfo[i];
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
            string[] stackTraces = ParseStackTrace(stackTrace);

            var list = new List<string>(stackTraces.Length);
            foreach (string line in stackTraces)
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

        public static ICollection<StackFrame> ScrubAndTruncate(StackFrame[] frames, int maxDepth)
        {
            List<StackFrame> list = new List<StackFrame>(Math.Min(frames.Length, maxDepth));
            foreach (StackFrame frame in frames)
            {
                if (frame.GetMethod().DeclaringType != null && !frame.GetMethod().DeclaringType.FullName.StartsWith("NewRelic"))
                {
                    if (list.Count >= maxDepth)
                    {
                        return list;
                    }
                    list.Add(frame);
                }
            }
            return list;
        }

        public static string ToString(StackFrame frame)
        {
            MethodBase method = frame.GetMethod();
            string typeName = method.DeclaringType == null ? "null" : method.DeclaringType.FullName;
            return string.Format("{0}.{1}({2}:{3})", typeName, method.Name, frame.GetFileName(), frame.GetFileLineNumber());
        }

        public static ICollection<string> ToStringList(ICollection<StackFrame> stackFrames)
        {
            List<string> stringList = new List<string>(stackFrames.Count);
            foreach (StackFrame frame in stackFrames)
                stringList.Add(ToString(frame));
            return stringList;
        }

        public static object ToJson(Exception ex)
        {
            IDictionary<string, string> exception = new Dictionary<string, string>();
            exception.Add("message", ex.Message);
            exception.Add("backtrace", ex.StackTrace);

            IDictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("exception", exception);

            return dict;
        }

    }
}

