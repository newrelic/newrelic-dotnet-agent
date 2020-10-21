// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using MoreLinq;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
    [TestFixture]
    public class TransactionMetadataTests
    {
        [Test]
        public void Build_HasEmptyPathHashIfNeverSet()
        {
            var transactionMetadata = new TransactionMetadata();
            var immutableMetadata = transactionMetadata.ConvertToImmutableMetadata();

            NrAssert.Multiple(
                () => Assert.AreEqual(null, immutableMetadata.CrossApplicationPathHash),
                () => Assert.AreEqual(0, immutableMetadata.CrossApplicationAlternatePathHashes.Count())
                );
        }

        [Test]
        public void Getters_HttpResponseStatusCode()
        {
            var metadata = new TransactionMetadata();
            Assert.IsNull(metadata.HttpResponseStatusCode);

            metadata.SetHttpResponseStatusCode(200, null, Mock.Create<IErrorService>());
            Assert.AreEqual(200, metadata.HttpResponseStatusCode);

            metadata.SetHttpResponseStatusCode(400, null, Mock.Create<IErrorService>());
            Assert.AreEqual(400, metadata.HttpResponseStatusCode);
            var immutableMetadata = metadata.ConvertToImmutableMetadata();
            Assert.AreEqual(400, immutableMetadata.HttpResponseStatusCode);
            Assert.IsNull(immutableMetadata.HttpResponseSubStatusCode);

            metadata.SetHttpResponseStatusCode(404, 420, Mock.Create<IErrorService>());
            Assert.AreEqual(404, metadata.HttpResponseStatusCode);
            immutableMetadata = metadata.ConvertToImmutableMetadata();
            Assert.AreEqual(404, immutableMetadata.HttpResponseStatusCode);
            Assert.AreEqual(420, immutableMetadata.HttpResponseSubStatusCode);
        }

        [Test]
        public void Getters_QueueTime()
        {
            var metadata = new TransactionMetadata();
            Assert.IsNull(metadata.QueueTime);

            metadata.SetQueueTime(TimeSpan.FromSeconds(20));
            Assert.AreEqual(20, metadata.QueueTime?.Seconds);

            metadata.SetQueueTime(TimeSpan.FromSeconds(55));
            Assert.AreEqual(55, metadata.QueueTime?.Seconds);
        }

        [Test]
        public void Getters_CATContentLength()
        {
            var metadata = new TransactionMetadata();
            Assert.AreEqual(-1, metadata.GetCrossApplicationReferrerContentLength());

            metadata.SetCrossApplicationReferrerContentLength(44444);
            Assert.AreEqual(44444, metadata.GetCrossApplicationReferrerContentLength());

            metadata.SetCrossApplicationReferrerContentLength(5432);
            Assert.AreEqual(5432, metadata.GetCrossApplicationReferrerContentLength());
        }

        [Test]
        public void Build_HasZeroAlternatePathHashesIfSetOnce()
        {
            var metadata = new TransactionMetadata();
            metadata.SetCrossApplicationPathHash("pathHash1");
            var immutableMetadata = metadata.ConvertToImmutableMetadata();

            NrAssert.Multiple(
                () => Assert.AreEqual("pathHash1", immutableMetadata.CrossApplicationPathHash),
                () => Assert.AreEqual(0, immutableMetadata.CrossApplicationAlternatePathHashes.Count())
                );
        }

        [Test]
        public void Build_PutsAllPathHashesIntoAlternatePathHashes_ExceptLatest()
        {
            var metadata = new TransactionMetadata();
            metadata.SetCrossApplicationPathHash("pathHash1");
            metadata.SetCrossApplicationPathHash("pathHash2");
            metadata.SetCrossApplicationPathHash("pathHash3");
            var immutableMetadata = metadata.ConvertToImmutableMetadata();

            NrAssert.Multiple(
                () => Assert.AreEqual("pathHash3", immutableMetadata.CrossApplicationPathHash),
                () => Assert.AreEqual(2, immutableMetadata.CrossApplicationAlternatePathHashes.Count()),
                () => Assert.IsTrue(immutableMetadata.CrossApplicationAlternatePathHashes.Contains("pathHash1")),
                () => Assert.IsTrue(immutableMetadata.CrossApplicationAlternatePathHashes.Contains("pathHash2"))
                );
        }

        [Test]
        public void Build_DoesNotKeepDuplicatesOfPathHashes()
        {
            var metadata = new TransactionMetadata();
            metadata.SetCrossApplicationPathHash("pathHash1");
            metadata.SetCrossApplicationPathHash("pathHash2");
            metadata.SetCrossApplicationPathHash("pathHash1");
            metadata.SetCrossApplicationPathHash("pathHash3");
            metadata.SetCrossApplicationPathHash("pathHash1");
            var immutableMetadata = metadata.ConvertToImmutableMetadata();

            NrAssert.Multiple(
                () => Assert.AreEqual("pathHash1", immutableMetadata.CrossApplicationPathHash),
                () => Assert.AreEqual(2, immutableMetadata.CrossApplicationAlternatePathHashes.Count()),
                () => Assert.IsTrue(immutableMetadata.CrossApplicationAlternatePathHashes.Contains("pathHash2")),
                () => Assert.IsTrue(immutableMetadata.CrossApplicationAlternatePathHashes.Contains("pathHash3"))
                );
        }

        [Test]
        public void Build_OnlyRetainsACertainNumberOfAlternatePathHashes()
        {
            var maxPathHashes = PathHashMaker.AlternatePathHashMaxSize;

            var transactionMetadata = new TransactionMetadata();
            Enumerable.Range(0, maxPathHashes + 2).ForEach(number => transactionMetadata.SetCrossApplicationPathHash($"pathHash{number}"));
            var immutableMetadata = transactionMetadata.ConvertToImmutableMetadata();

            NrAssert.Multiple(
                () => Assert.AreEqual($"pathHash{PathHashMaker.AlternatePathHashMaxSize + 1}", immutableMetadata.CrossApplicationPathHash),
                () => Assert.AreEqual(PathHashMaker.AlternatePathHashMaxSize, immutableMetadata.CrossApplicationAlternatePathHashes.Count()),
                () => Assert.IsFalse(immutableMetadata.CrossApplicationAlternatePathHashes.Contains($"pathHash{PathHashMaker.AlternatePathHashMaxSize + 1}"))
                );
        }

        
    }
}
