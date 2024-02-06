// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NewRelic.Agent.Core;
using NewRelic.Agent.Core.Utilities;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utils
{
    [TestFixture]
    public class StackTracesTest
    {
        [Test]
        public static void TestParseStackTrace()
        {
            ICollection<string> stack = StackTraces.ParseStackTrace(string.Join(System.Environment.NewLine,
                        new string[] {
                       "  at Microsoft.ServiceModel.Samples.CalculatorService.Add(Double n1, Double n2)",
                       "  at SyncInvokeAdd(Object , Object[] , Object[] )",
                       "  at System.ServiceModel.Dispatcher.SyncMethodInvoker.Invoke(Object instance, Object[] inputs, Object[]& outputs)",
                       "  at System.ServiceModel.Dispatcher.DispatchOperationRuntime.InvokeBegin(MessageRpc& rpc)",
                       "  at System.ServiceModel.Dispatcher.ImmutableDispatchRuntime.ProcessMessage5(MessageRpc& rpc)"}));
            Assert.That(stack, Has.Count.EqualTo(5));
        }

        [Test]
        public static void TestScrub()
        {
            StackFrame[] stackTraces = GetStackTrace().GetFrames();
            IList<StackFrame> frames = StackTraces.ScrubAndTruncate(stackTraces, 300);
            Assert.That(frames, Has.Count.Not.EqualTo(stackTraces.Length));
        }

        [Test]
        public static void TestScrubAndTruncate()
        {
            ICollection<string> stack = StackTraces.ScrubAndTruncate(string.Join(System.Environment.NewLine,
                        new string[] {
                       "  at NewRelic.Agent.Dude()",
                       "  at Microsoft.ServiceModel.Samples.CalculatorService.Add(Double n1, Double n2)",
                       "  at SyncInvokeAdd(Object , Object[] , Object[] )",
                       "  at System.ServiceModel.Dispatcher.SyncMethodInvoker.Invoke(Object instance, Object[] inputs, Object[]& outputs)",
                       "  at System.ServiceModel.Dispatcher.DispatchOperationRuntime.InvokeBegin(MessageRpc& rpc)",
                       "  at System.ServiceModel.Dispatcher.ImmutableDispatchRuntime.ProcessMessage5(MessageRpc& rpc)"}), 10);
            Assert.That(stack, Has.Count.EqualTo(5));
        }

        [Test]
        public static void TestScrubNullString()
        {
            ICollection<string> frames = StackTraces.ScrubAndTruncate((string)null, 300);
            Assert.That(frames, Is.Empty);
        }

        [Test]
        public static void TestScrubBadString()
        {
            ICollection<string> frames = StackTraces.ScrubAndTruncate(
                 string.Join(System.Environment.NewLine, new string[] { "", "", null, "" }), 300);
            Assert.That(frames, Has.Count.EqualTo(4));
        }

        [Test]
        public static void TestScrubException()
        {
            try
            {
                Test.Test.TestThrow();
                Assert.Fail();
            }
            catch (Exception ex)
            {
                ICollection<string> frames = StackTraces.ScrubAndTruncate(ex, 300);
                Console.WriteLine("Here's the pretty stack :\n " + ex.StackTrace);
                foreach (string frame in frames)
                {
                    Console.WriteLine(frame);
                }
                Assert.That(frames, Has.Count.EqualTo(10), ex.StackTrace);
            }

        }

        [Test]
        public static void TestMethodToStringNoParameters()
        {
            MethodInfo method = typeof(StackTrace).GetMethod("GetFrames");
            string str = StackTraces.MethodToString(method);

            Assert.That(str, Is.EqualTo("System.Diagnostics.StackTrace.GetFrames()"));
        }

        [Test]
        public static void TestMethodToStringsOneParameter()
        {
            MethodInfo method = typeof(StackTrace).GetMethod("GetFrame");
            string str = StackTraces.MethodToString(method);

            Assert.That(str, Is.EqualTo("System.Diagnostics.StackTrace.GetFrame(System.Int32 index)"));
        }

        [Test]
        public static void TestMethodToStringTwoParameters()
        {
            MethodBase method = typeof(StackTrace).GetConstructor(new Type[] { typeof(Exception), typeof(int) });
            string str = StackTraces.MethodToString(method);

            Assert.That(str, Is.EqualTo("System.Diagnostics.StackTrace..ctor(System.Exception e,System.Int32 skipFrames)"));
        }

        [Test]
        public static void TestTruncate()
        {
            StackFrame[] stackTraces = GetStackTrace().GetFrames();
            ICollection<StackFrame> frames = StackTraces.ScrubAndTruncate(stackTraces, 3);
            Assert.That(frames, Has.Count.EqualTo(3));
        }

        [Test]
        public static void TestTruncateWithZeroMax()
        {
            StackFrame[] stackTraces = GetStackTrace().GetFrames();
            ICollection<StackFrame> frames = StackTraces.ScrubAndTruncate(stackTraces, 0);
            Assert.That(frames, Is.Empty);
        }

        // The purpose of this test is to verify that we do something reasonable and don't throw an exception
        // if a max depth greater than the available frames is supplied
        [Test]
        public static void TestTruncateWithMaxDepthGreaterThanInputList()
        {
            StackFrame[] stackTraces = GetStackTrace().GetFrames();
            ICollection<StackFrame> frames = StackTraces.ScrubAndTruncate(stackTraces, stackTraces.Length + 666);
            Assert.That(frames.Count, Is.LessThanOrEqualTo(stackTraces.Length));
        }

        [Test]
        public static void TestToString()
        {
            StackFrame frame = new StackFrame("dude", 6, 6);
            string str = StackTraces.ToString(frame);
            Assert.That(str, Is.EqualTo("NewRelic.Agent.Core.Utils.StackTracesTest.TestToString(dude:6)"));
        }

        [Test]
        public static void TestListToString()
        {
            StackFrame frame = new StackFrame("dude", 6, 6);
            ICollection<string> strings = StackTraces.ToStringList(new StackFrame[] { frame });
            Assert.That(strings, Has.Count.EqualTo(1));
            IEnumerator<string> en = strings.GetEnumerator();
            Assert.Multiple(() =>
            {
                Assert.That(en.MoveNext());
                Assert.That(en.Current, Is.EqualTo(StackTraces.ToString(frame)));
            });
        }

        private static StackTrace GetStackTrace()
        {
            return new StackTrace();
        }
    }
}

namespace Test
{
    public class Test
    {
        public static void TestThrow()
        {
            try
            {
                Inner();
            }
            catch (Exception ex)
            {
                throw new DivideByZeroException("Dude", ex);
            }
        }

        public static void Inner()
        {
            try
            {
                if (typeof(Test).FullName.StartsWith("Test"))
                    throw new SimpleException("Test");
            }
            catch (Exception ex)
            {
                throw new InvalidCastException("Test", ex);
            }
        }
    }
}
