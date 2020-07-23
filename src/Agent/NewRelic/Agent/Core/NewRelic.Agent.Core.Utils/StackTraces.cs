using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Utils
{
    public static class StackTraces
    {
        [NotNull]
        public static ICollection<string> ScrubAndTruncate(Exception exception, int maxDepth)
        {
            if (null == exception)
            {
                return new List<string>(0);
            }
            var frames = new List<String>(maxDepth);

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
                frames.Add(String.Format("[{0}: {1}]", ex.GetType().Name, ex.Message));
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

        public static String MethodToString(MethodBase method)
        {
            return String.Format("{0}.{1}({2})", method.DeclaringType.FullName, method.Name, FormatMethodParameters(method.GetParameters()));
        }

        private static String FormatMethodParameters(ParameterInfo[] parameterInfo)
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

        [NotNull]
        public static ICollection<string> ScrubAndTruncate(string stackTrace, int maxDepth)
        {
            String[] stackTraces = ParseStackTrace(stackTrace);

            List<String> list = new List<String>(stackTraces.Length);
            foreach (String line in stackTraces)
            {
                if (line != null && line.IndexOf("at NewRelic.Agent", 0, Math.Min(20, line.Length)) < 0)
                {
                    list.Add('\t' + line);
                    if (list.Count == maxDepth)
                    {
                        return list;
                    }
                }
            }

            return list;
        }

        private static readonly String[] NEWLINE_SPLITTER = new String[] { System.Environment.NewLine };
        public static String[] ParseStackTrace(String stackTrace)
        {
            if (null == stackTrace)
            {
                return new String[0];
            }
            return stackTrace.Split(NEWLINE_SPLITTER, StringSplitOptions.None);
        }

        public static ICollection<StackFrame> ScrubAndTruncate(StackFrame[] frames, int maxDepth)
        {
            List<StackFrame> list = new List<StackFrame>(frames.Length);
            foreach (StackFrame frame in frames)
            {
                if (frame.GetMethod().DeclaringType != null && !frame.GetMethod().DeclaringType.FullName.StartsWith("NewRelic"))
                {
                    list.Add(frame);
                    if (list.Count == maxDepth)
                    {
                        return list;
                    }
                }
            }
            return list;
        }

        public static String ToString(StackFrame frame)
        {
            MethodBase method = frame.GetMethod();
            String typeName = method.DeclaringType == null ? "null" : method.DeclaringType.FullName;
            return String.Format("{0}.{1}({2}:{3})", typeName, method.Name, frame.GetFileName(), frame.GetFileLineNumber());
        }

        public static ICollection<String> ToStringList(ICollection<StackFrame> stackFrames)
        {
            List<String> stringList = new List<String>(stackFrames.Count);
            foreach (StackFrame frame in stackFrames)
                stringList.Add(ToString(frame));
            return stringList;
        }

        public static Object ToJson(Exception ex)
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

