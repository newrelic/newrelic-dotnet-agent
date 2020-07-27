using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
    [TestFixture]
    public class CustomSegmentDataTests
    {
        #region IsCombinableWith


        public static TypedSegment<CustomSegmentData> createCustomSegmentBuilder(TimeSpan start, TimeSpan duration, int uniqueId, int? parentId, MethodCallData methodCallData, IEnumerable<KeyValuePair<string, object>> enumerable, string name, bool combinable)
        {
            return new TypedSegment<CustomSegmentData>(start, duration,
                new TypedSegment<CustomSegmentData>(SimpleSegmentDataTests.createTransactionSegmentState(uniqueId, parentId), methodCallData, new CustomSegmentData(name), combinable));

        }

        [Test]
        public void IsCombinableWith_ReturnsTrue_ForIdenticalSegments()
        {
            var segment1 = createCustomSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);
            var segment2 = createCustomSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);

            Assert.IsTrue(segment1.IsCombinableWith(segment2));
        }


        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentCombinable()
        {
            var segment1 = createCustomSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);
            var segment2 = createCustomSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", false);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfBothNotCombinable()
        {
            var segment1 = createCustomSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", false);
            var segment2 = createCustomSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", false);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentHashCode()
        {
            var segment1 = createCustomSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);
            var segment2 = createCustomSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 2), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentTypeName()
        {
            var segment1 = createCustomSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);
            var segment2 = createCustomSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type2", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentMethodName()
        {
            var segment1 = createCustomSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);
            var segment2 = createCustomSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method2", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentName()
        {
            var segment1 = createCustomSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);
            var segment2 = createCustomSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name2", true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentSegmentType()
        {
            var segment1 = createCustomSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);
            var segment2 = MethodSegmentDataTests.createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "type", "method", true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        #endregion IsCombinableWith
    }
}
