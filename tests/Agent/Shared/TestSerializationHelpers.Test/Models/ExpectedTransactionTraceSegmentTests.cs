// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Tests.Models
{
    public class ExpectedTransactionTraceSegmentTests
    {
        private const string ClassName = "Namespace.Classname";

        [Fact]
        public void BackgroundThreadMethodOrderingMayNotMatter()
        {
            var expectedTraceTree = ExpectedTransactionTraceSegment.NewTree(ClassName, "RootMethod",
                ExpectedTransactionTraceSegment.NewSubtree("MetricNameRoot", ClassName, "RootMethod",
                    ExpectedTransactionTraceSegment.NewBackgroundThreadSubtree("MetricName1", ClassName, "Method1",
                        ExpectedTransactionTraceSegment.NewSubtree("MetricName11", ClassName, "Method11"),
                        ExpectedTransactionTraceSegment.NewSubtree("MetricName12", ClassName, "Method12")
                    ),
                    ExpectedTransactionTraceSegment.NewBackgroundThreadSubtree("MetricName2", ClassName, "Method2",
                        ExpectedTransactionTraceSegment.NewSubtree("MetricName21", ClassName, "Method21"),
                        ExpectedTransactionTraceSegment.NewSubtree("MetricName22", ClassName, "Method22")
                    ),
                    ExpectedTransactionTraceSegment.NewBackgroundThreadSubtree("MetricName3", ClassName, "Method3",
                        ExpectedTransactionTraceSegment.NewSubtree("MetricName31", ClassName, "Method31"),
                        ExpectedTransactionTraceSegment.NewSubtree("MetricName32", ClassName, "Method32")
                    )
                )
            );

            var actualTraceTree = TestTransactionTraceSegment.NewTree(ClassName, "RootMethod",
                TestTransactionTraceSegment.NewSubtree("MetricNameRoot", ClassName, "RootMethod",
                    TestTransactionTraceSegment.NewSubtree("MetricName3", ClassName, "Method3",
                        TestTransactionTraceSegment.NewSubtree("MetricName31", ClassName, "Method31"),
                        TestTransactionTraceSegment.NewSubtree("MetricName32", ClassName, "Method32")
                    ),
                    TestTransactionTraceSegment.NewSubtree("MetricName2", ClassName, "Method2",
                        TestTransactionTraceSegment.NewSubtree("MetricName21", ClassName, "Method21"),
                        TestTransactionTraceSegment.NewSubtree("MetricName22", ClassName, "Method22")
                    ),
                    TestTransactionTraceSegment.NewSubtree("MetricName1", ClassName, "Method1",
                        TestTransactionTraceSegment.NewSubtree("MetricName11", ClassName, "Method11"),
                        TestTransactionTraceSegment.NewSubtree("MetricName12", ClassName, "Method12")
                    )
                )
            );

            var result = expectedTraceTree.CompareToActualTransactionTrace(actualTraceTree);
            Assert.True(result.IsEquivalent, result.Diff);
        }

        [Fact]
        public void BackgroundThreadMethodShouldNotOccurBeforeAnyPreceedingSyncMethods()
        {
            var expectedTraceTree = ExpectedTransactionTraceSegment.NewTree(ClassName, "RootMethod",
                ExpectedTransactionTraceSegment.NewSubtree("MetricNameRoot", ClassName, "RootMethod",
                    ExpectedTransactionTraceSegment.NewBackgroundThreadSubtree("MetricName1", ClassName, "Method1",
                        ExpectedTransactionTraceSegment.NewSubtree("MetricName11", ClassName, "Method11"),
                        ExpectedTransactionTraceSegment.NewSubtree("MetricName12", ClassName, "Method12")
                    ),
                    ExpectedTransactionTraceSegment.NewSubtree("MetricName2", ClassName, "Method2",
                        ExpectedTransactionTraceSegment.NewSubtree("MetricName21", ClassName, "Method21"),
                        ExpectedTransactionTraceSegment.NewSubtree("MetricName22", ClassName, "Method22")
                    ),
                    // Method3 should not occur before synchronous Method2 in a transaction trace
                    ExpectedTransactionTraceSegment.NewBackgroundThreadSubtree("MetricName3", ClassName, "Method3",
                        ExpectedTransactionTraceSegment.NewSubtree("MetricName31", ClassName, "Method31"),
                        ExpectedTransactionTraceSegment.NewSubtree("MetricName32", ClassName, "Method32")
                    )
                )
            );

            var actualTraceTree = TestTransactionTraceSegment.NewTree(ClassName, "RootMethod",
                TestTransactionTraceSegment.NewSubtree("MetricNameRoot", ClassName, "RootMethod",
                    TestTransactionTraceSegment.NewSubtree("MetricName3", ClassName, "Method3",
                        TestTransactionTraceSegment.NewSubtree("MetricName31", ClassName, "Method31"),
                        TestTransactionTraceSegment.NewSubtree("MetricName32", ClassName, "Method32")
                    ),
                    TestTransactionTraceSegment.NewSubtree("MetricName2", ClassName, "Method2",
                        TestTransactionTraceSegment.NewSubtree("MetricName21", ClassName, "Method21"),
                        TestTransactionTraceSegment.NewSubtree("MetricName22", ClassName, "Method22")
                    ),
                    TestTransactionTraceSegment.NewSubtree("MetricName1", ClassName, "Method1",
                        TestTransactionTraceSegment.NewSubtree("MetricName11", ClassName, "Method11"),
                        TestTransactionTraceSegment.NewSubtree("MetricName12", ClassName, "Method12")
                    )
                )
            );

            var result = expectedTraceTree.CompareToActualTransactionTrace(actualTraceTree);
            Assert.False(result.IsEquivalent, result.Diff);
        }

        [Fact]
        public void ShouldEnsureCorrectIdenticalBackgroundThreadSubtreeCount()
        {
            var expectedTraceTree = ExpectedTransactionTraceSegment.NewTree(ClassName, "RootMethod",
                ExpectedTransactionTraceSegment.NewSubtree("MetricNameRoot", ClassName, "RootMethod",
                    ExpectedTransactionTraceSegment.NewBackgroundThreadSubtree("MetricName1", ClassName, "Method1",
                        ExpectedTransactionTraceSegment.NewSubtree("MetricName11", ClassName, "Method11"),
                        ExpectedTransactionTraceSegment.NewSubtree("MetricName12", ClassName, "Method12")
                    ),
                    ExpectedTransactionTraceSegment.NewBackgroundThreadSubtree("MetricName1", ClassName, "Method1",
                        ExpectedTransactionTraceSegment.NewSubtree("MetricName11", ClassName, "Method11"),
                        ExpectedTransactionTraceSegment.NewSubtree("MetricName12", ClassName, "Method12")
                    ),
                    ExpectedTransactionTraceSegment.NewSubtree("MetricName2", ClassName, "Method2",
                        ExpectedTransactionTraceSegment.NewSubtree("MetricName21", ClassName, "Method21"),
                        ExpectedTransactionTraceSegment.NewSubtree("MetricName22", ClassName, "Method22")
                    )
                )
            );

            var actualTraceTree = TestTransactionTraceSegment.NewTree(ClassName, "RootMethod",
                TestTransactionTraceSegment.NewSubtree("MetricNameRoot", ClassName, "RootMethod",
                    TestTransactionTraceSegment.NewSubtree("MetricName1", ClassName, "Method1",
                        TestTransactionTraceSegment.NewSubtree("MetricName11", ClassName, "Method11"),
                        TestTransactionTraceSegment.NewSubtree("MetricName12", ClassName, "Method12")
                    ),
                    TestTransactionTraceSegment.NewSubtree("MetricName2", ClassName, "Method2",
                        TestTransactionTraceSegment.NewSubtree("MetricName21", ClassName, "Method21"),
                        TestTransactionTraceSegment.NewSubtree("MetricName22", ClassName, "Method22")
                    )
                )
            );

            var result = expectedTraceTree.CompareToActualTransactionTrace(actualTraceTree);
            Assert.False(result.IsEquivalent, result.Diff);
        }
    }

    public class TestTransactionTraceSegment : TransactionTraceSegment
    {
        public TestTransactionTraceSegment(string name, string className, string methodName, IEnumerable<TestTransactionTraceSegment> childSegments)
            : base(TimeSpan.Zero, TimeSpan.Zero, name, null, childSegments, className, methodName)
        {
        }

        public static TestTransactionTraceSegment NewTree(string className, string methodName, params TestTransactionTraceSegment[] subtrees)
        {
            return new TestTransactionTraceSegment("ROOT", className, methodName, new List<TestTransactionTraceSegment>
            {
                new TestTransactionTraceSegment("Transaction", className, methodName, subtrees)
            });
        }

        public static TestTransactionTraceSegment NewSubtree(string name, string className, string methodName, params TestTransactionTraceSegment[] subtrees)
        {
            return new TestTransactionTraceSegment(name, className, methodName, subtrees);
        }
    }
}
