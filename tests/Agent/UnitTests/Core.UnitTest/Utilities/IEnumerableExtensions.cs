// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utilities
{
    [TestFixture]
    public class Class_IEnumerableExtensions
    {
        [Test]
        [TestCase(ExpectedResult = true, Description = "Single element sequence")]
        [TestCase(5, ExpectedResult = true, Description = "Single element sequence")]
        [TestCase(5, 6, 7, ExpectedResult = true, Description = "Contiguous sequence")]
        [TestCase(5, 7, 6, ExpectedResult = false, Description = "Out of order sequence")]
        [TestCase(5, 6, 8, 9, ExpectedResult = false, Description = "Broken sequence")]
        public bool IsSequential_ReturnsTrue_IfSequenceIsSequential(params int[] list)
        {
            var isSequential = list.Select(Convert.ToUInt32).IsSequential();

            return isSequential;
        }

        [Test]
        [TestCase(ExpectedResult = true, Description = "Single element sequence")]
        [TestCase(5, ExpectedResult = true, Description = "Single element sequence")]
        [TestCase(5, 6, 7, ExpectedResult = true, Description = "Contiguous sequence")]
        [TestCase(5, 7, 6, ExpectedResult = false, Description = "Out of order sequence")]
        [TestCase(5, 6, 8, 9, ExpectedResult = false, Description = "Broken sequence")]
        public bool IsSequentialWithPredicate_ReturnsTrue_IfSequenceIsSequential(params int[] list)
        {
            var tupleList = list.Select(number => new Tuple<uint, string>((uint)number, "blah"));

            var isSequential = tupleList.IsSequential(tuple => tuple.Item1);

            return isSequential;
        }
    }
}
