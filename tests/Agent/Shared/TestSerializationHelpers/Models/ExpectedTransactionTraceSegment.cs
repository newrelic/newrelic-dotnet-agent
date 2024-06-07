// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    public class ExpectedTransactionTraceSegment
    {
        public ExpectedTransactionTraceSegment(string name, string className, string methodName, bool orderedChild, IList<ExpectedTransactionTraceSegment> childSegments)
        {
            Name = name;
            ChildSegments = childSegments;
            ClassName = className;
            MethodName = methodName;
            OrderedChild = orderedChild;
        }

        public string Name { get; set; }
        public string ClassName { get; set; }
        public string MethodName { get; set; }
        public bool OrderedChild { get; set; }
        public IList<ExpectedTransactionTraceSegment> ChildSegments { get; set; }

        public TransactionTraceComparisonResult CompareToActualTransactionTrace(TransactionTraceSegment compare)
        {
            var equivalent = CompareToActualTransactionTrace(this, compare);
            return new TransactionTraceComparisonResult(equivalent, PrintTree(compare));
        }

        private bool CompareToActualTransactionTrace(ExpectedTransactionTraceSegment expected, TransactionTraceSegment actual)
        {
            var expectedChildSegments = expected.ChildSegments.ToArray();
            var actualChildSegments = actual.ChildSegments.ToArray();
            if (expectedChildSegments.Length != actualChildSegments.Length) return false;

            var structureMatches = true;
            for (var i = 0; structureMatches && i < expectedChildSegments.Length; ++i)
            {
                if (expectedChildSegments[i].OrderedChild)
                {
                    structureMatches = CompareToActualTransactionTrace(expectedChildSegments[i], actualChildSegments[i]);
                }
                else
                {
                    var foundMatchingSubtree = false;
                    var isOrderedChild = false;
                    for (var offset = i; !foundMatchingSubtree && !isOrderedChild && offset < expectedChildSegments.Length; ++offset)
                    {
                        isOrderedChild = expectedChildSegments[offset].OrderedChild;
                        foundMatchingSubtree = CompareToActualTransactionTrace(expectedChildSegments[offset], actualChildSegments[i]);
                        if (foundMatchingSubtree)
                        {
                            var t = expectedChildSegments[i];
                            expectedChildSegments[i] = expectedChildSegments[offset];
                            expectedChildSegments[offset] = t;
                        }
                    }

                    structureMatches = foundMatchingSubtree;
                }
            }

            return structureMatches
                && expected.Name == actual.Name
                && expected.ClassName == actual.ClassName
                && expected.MethodName == actual.MethodName;
        }

        private string PrintTree(TransactionTraceSegment actual)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("Expected:");
            sb.Append($"{Name}, {ClassName}, {MethodName}");
            foreach (var segment in ChildSegments)
            {
                PrintTree(segment, 1, sb);
            }
            sb.AppendLine();
            sb.AppendLine("Actual:");
            sb.Append($"{actual.Name}, {actual.ClassName}, {actual.MethodName}");
            foreach (var segment in actual.ChildSegments)
            {
                PrintTree(segment, 1, sb);
            }
            return sb.ToString();
        }

        private string PrintTree(TransactionTraceSegment actual, int depth, StringBuilder sb)
        {
            sb.AppendLine();
            for (int i = 0; i < depth; ++i)
            {
                sb.Append("    ");
            }
            sb.Append($"{actual.Name}, {actual.ClassName}, {actual.MethodName}");
            foreach (var segment in actual.ChildSegments)
            {
                PrintTree(segment, depth + 1, sb);
            }
            return sb.ToString();
        }

        private string PrintTree(ExpectedTransactionTraceSegment expected, int depth, StringBuilder sb)
        {
            sb.AppendLine();
            for (int i = 0; i < depth; ++i)
            {
                sb.Append("    ");
            }
            sb.Append($"{expected.Name}, {expected.ClassName}, {expected.MethodName}");
            foreach (var segment in expected.ChildSegments)
            {
                PrintTree(segment, depth + 1, sb);
            }
            return sb.ToString();
        }

        public static ExpectedTransactionTraceSegment NewTree(string className, string methodName, params ExpectedTransactionTraceSegment[] subtrees)
        {
            return new ExpectedTransactionTraceSegment("ROOT", className, methodName, true, new List<ExpectedTransactionTraceSegment>
            {
                new ExpectedTransactionTraceSegment("Transaction", className, methodName, true, subtrees)
            });
        }

        public static ExpectedTransactionTraceSegment NewSubtree(string name, string className, string methodName, params ExpectedTransactionTraceSegment[] subtrees)
        {
            return new ExpectedTransactionTraceSegment(name, className, methodName, true, subtrees);
        }

        public static ExpectedTransactionTraceSegment NewBackgroundThreadSubtree(string name, string className, string methodName, params ExpectedTransactionTraceSegment[] subtrees)
        {
            return new ExpectedTransactionTraceSegment(name, className, methodName, false, subtrees);
        }
    }
}
