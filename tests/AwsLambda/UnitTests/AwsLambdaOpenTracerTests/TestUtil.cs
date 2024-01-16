// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.OpenTracing.AmazonLambda.State;
using NewRelic.OpenTracing.AmazonLambda.Util;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace NewRelic.Tests.AwsLambda.AwsLambdaOpenTracerTests
{
    public class TestUtil
    {
        internal static LambdaSpan CreateSpan(string operationName, DateTimeOffset timestamp, IDictionary<string, object> tags, LambdaSpan parentSpan, string guid)
        {
            var span = new LambdaSpan(operationName, timestamp, tags, parentSpan, guid);
            LambdaSpanContext context = new LambdaSpanContext(span);
            span.SetContext(context);
            return span;
        }
        internal static LambdaSpan CreateRootSpan(string operationName, DateTimeOffset timestamp, IDictionary<string, object> tags, string guid, ILogger logger = null, IFileSystemManager fileSystemManager = null)
        {
            if (logger == null)
            {
                logger = new MockLogger();
            }

            if (fileSystemManager == null)
            {
                fileSystemManager = new MockFileSystemManager();
            }

            var rootSpan = new LambdaRootSpan(operationName, timestamp, tags, guid, new DataCollector(logger, false, fileSystemManager), new TransactionState(), new PrioritySamplingState(), new DistributedTracingState());
            LambdaSpanContext context = new LambdaSpanContext(rootSpan);
            rootSpan.SetContext(context);
            return rootSpan;
        }

        public static string DecodeAndDecompressNewRelicPayload(string source)
        {
            var byteArray = Convert.FromBase64String(source);

            using (GZipStream stream = new GZipStream(new MemoryStream(byteArray),
                       CompressionMode.Decompress))
            {
                const int size = 4096;
                byte[] buffer = new byte[size];
                using (MemoryStream memory = new MemoryStream())
                {
                    int count = 0;
                    do
                    {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0)
                        {
                            memory.Write(buffer, 0, count);
                        }
                    }
                    while (count > 0);
                    return Encoding.UTF8.GetString(memory.ToArray());
                }
            }
        }

        public static int CountStringOccurrences(string text, string pattern)
        {
            // Loop through all instances of the string 'text'.
            int count = 0;
            int i = 0;
            while ((i = text.IndexOf(pattern, i)) != -1)
            {
                i += pattern.Length;
                count++;
            }
            return count;
        }

    }
}
