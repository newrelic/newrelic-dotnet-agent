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
            var transactionMetadata = new TransactionMetadata("transactionGuid");
            var immutableMetadata = transactionMetadata.ConvertToImmutableMetadata();

            NrAssert.Multiple(
                () => Assert.That(immutableMetadata.CrossApplicationPathHash, Is.EqualTo(null)),
                () => Assert.That(immutableMetadata.CrossApplicationAlternatePathHashes.Count(), Is.EqualTo(0))
                );
        }

        [Test]
        public void Getters_HttpResponseStatusCode()
        {
            var metadata = new TransactionMetadata("transactionGuid");
            Assert.That(metadata.HttpResponseStatusCode, Is.Null);

            metadata.SetHttpResponseStatusCode(200, null, Mock.Create<IErrorService>());
            Assert.That(metadata.HttpResponseStatusCode, Is.EqualTo(200));

            metadata.SetHttpResponseStatusCode(400, null, Mock.Create<IErrorService>());
            Assert.That(metadata.HttpResponseStatusCode, Is.EqualTo(400));
            var immutableMetadata = metadata.ConvertToImmutableMetadata();
            Assert.Multiple(() =>
            {
                Assert.That(immutableMetadata.HttpResponseStatusCode, Is.EqualTo(400));
                Assert.That(immutableMetadata.HttpResponseSubStatusCode, Is.Null);
            });

            metadata.SetHttpResponseStatusCode(404, 420, Mock.Create<IErrorService>());
            Assert.That(metadata.HttpResponseStatusCode, Is.EqualTo(404));
            immutableMetadata = metadata.ConvertToImmutableMetadata();
            Assert.Multiple(() =>
            {
                Assert.That(immutableMetadata.HttpResponseStatusCode, Is.EqualTo(404));
                Assert.That(immutableMetadata.HttpResponseSubStatusCode, Is.EqualTo(420));
            });
        }

        [Test]
        public void Getters_QueueTime()
        {
            var metadata = new TransactionMetadata("transactionGuid");
            Assert.That(metadata.QueueTime, Is.Null);

            metadata.SetQueueTime(TimeSpan.FromSeconds(20));
            Assert.That(metadata.QueueTime?.Seconds, Is.EqualTo(20));

            metadata.SetQueueTime(TimeSpan.FromSeconds(55));
            Assert.That(metadata.QueueTime?.Seconds, Is.EqualTo(55));
        }

        [Test]
        public void Getters_CATContentLength()
        {
            var metadata = new TransactionMetadata("transactionGuid");
            Assert.That(metadata.GetCrossApplicationReferrerContentLength(), Is.EqualTo(-1));

            metadata.SetCrossApplicationReferrerContentLength(44444);
            Assert.That(metadata.GetCrossApplicationReferrerContentLength(), Is.EqualTo(44444));

            metadata.SetCrossApplicationReferrerContentLength(5432);
            Assert.That(metadata.GetCrossApplicationReferrerContentLength(), Is.EqualTo(5432));
        }

        [Test]
        public void Build_HasZeroAlternatePathHashesIfSetOnce()
        {
            var metadata = new TransactionMetadata("transactionGuid");
            metadata.SetCrossApplicationPathHash("pathHash1");
            var immutableMetadata = metadata.ConvertToImmutableMetadata();

            NrAssert.Multiple(
                () => Assert.That(immutableMetadata.CrossApplicationPathHash, Is.EqualTo("pathHash1")),
                () => Assert.That(immutableMetadata.CrossApplicationAlternatePathHashes.Count(), Is.EqualTo(0))
                );
        }

        [Test]
        public void Build_PutsAllPathHashesIntoAlternatePathHashes_ExceptLatest()
        {
            var metadata = new TransactionMetadata("transactionGuid");
            metadata.SetCrossApplicationPathHash("pathHash1");
            metadata.SetCrossApplicationPathHash("pathHash2");
            metadata.SetCrossApplicationPathHash("pathHash3");
            var immutableMetadata = metadata.ConvertToImmutableMetadata();

            NrAssert.Multiple(
                () => Assert.That(immutableMetadata.CrossApplicationPathHash, Is.EqualTo("pathHash3")),
                () => Assert.That(immutableMetadata.CrossApplicationAlternatePathHashes.Count(), Is.EqualTo(2)),
                () => Assert.That(immutableMetadata.CrossApplicationAlternatePathHashes, Does.Contain("pathHash1")),
                () => Assert.That(immutableMetadata.CrossApplicationAlternatePathHashes, Does.Contain("pathHash2"))
                );
        }

        [Test]
        public void Build_DoesNotKeepDuplicatesOfPathHashes()
        {
            var metadata = new TransactionMetadata("transactionGuid");
            metadata.SetCrossApplicationPathHash("pathHash1");
            metadata.SetCrossApplicationPathHash("pathHash2");
            metadata.SetCrossApplicationPathHash("pathHash1");
            metadata.SetCrossApplicationPathHash("pathHash3");
            metadata.SetCrossApplicationPathHash("pathHash1");
            var immutableMetadata = metadata.ConvertToImmutableMetadata();

            NrAssert.Multiple(
                () => Assert.That(immutableMetadata.CrossApplicationPathHash, Is.EqualTo("pathHash1")),
                () => Assert.That(immutableMetadata.CrossApplicationAlternatePathHashes.Count(), Is.EqualTo(2)),
                () => Assert.That(immutableMetadata.CrossApplicationAlternatePathHashes, Does.Contain("pathHash2")),
                () => Assert.That(immutableMetadata.CrossApplicationAlternatePathHashes, Does.Contain("pathHash3"))
                );
        }

        [Test]
        public void Build_OnlyRetainsACertainNumberOfAlternatePathHashes()
        {
            var maxPathHashes = PathHashMaker.AlternatePathHashMaxSize;

            var transactionMetadata = new TransactionMetadata("transactionGuid");
            Enumerable.Range(0, maxPathHashes + 2).ForEach(number => transactionMetadata.SetCrossApplicationPathHash($"pathHash{number}"));
            var immutableMetadata = transactionMetadata.ConvertToImmutableMetadata();

            NrAssert.Multiple(
                () => Assert.That(immutableMetadata.CrossApplicationPathHash, Is.EqualTo($"pathHash{PathHashMaker.AlternatePathHashMaxSize + 1}")),
                () => Assert.That(immutableMetadata.CrossApplicationAlternatePathHashes.Count(), Is.EqualTo(PathHashMaker.AlternatePathHashMaxSize)),
                () => Assert.That(immutableMetadata.CrossApplicationAlternatePathHashes, Does.Not.Contain($"pathHash{PathHashMaker.AlternatePathHashMaxSize + 1}"))
                );
        }

        
    }
}
