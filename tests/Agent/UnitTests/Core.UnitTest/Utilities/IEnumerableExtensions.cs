using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utilities
{
    [TestFixture]
    // ReSharper disable InconsistentNaming
    public class Class_IEnumerableExtensions
    // ReSharper restore InconsistentNaming
    {
        [Test]
        [TestCase(ExpectedResult = true, Description = "Single element sequence")]
        [TestCase(5, ExpectedResult = true, Description = "Single element sequence")]
        [TestCase(5, 6, 7, ExpectedResult = true, Description = "Contiguous sequence")]
        [TestCase(5, 7, 6, ExpectedResult = false, Description = "Out of order sequence")]
        [TestCase(5, 6, 8, 9, ExpectedResult = false, Description = "Broken sequence")]
        public Boolean IsSequential_ReturnsTrue_IfSequenceIsSequential(params Int32[] list)
        {
            var isSequential = list.Cast<UInt32>().IsSequential();

            return isSequential;
        }

        [Test]
        [TestCase(ExpectedResult = true, Description = "Single element sequence")]
        [TestCase(5, ExpectedResult = true, Description = "Single element sequence")]
        [TestCase(5, 6, 7, ExpectedResult = true, Description = "Contiguous sequence")]
        [TestCase(5, 7, 6, ExpectedResult = false, Description = "Out of order sequence")]
        [TestCase(5, 6, 8, 9, ExpectedResult = false, Description = "Broken sequence")]
        public Boolean IsSequentialWithPredicate_ReturnsTrue_IfSequenceIsSequential(params Int32[] list)
        {
            var tupleList = list.Select(number => new Tuple<UInt32, String>((UInt32)number, "blah"));

            var isSequential = tupleList.IsSequential(tuple => tuple.Item1);

            return isSequential;
        }
    }
}
