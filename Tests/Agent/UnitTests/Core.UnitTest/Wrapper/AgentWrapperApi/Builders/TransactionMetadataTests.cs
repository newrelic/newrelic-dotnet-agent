using System;
using System.Linq;
using MoreLinq;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

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

			metadata.SetHttpResponseStatusCode(200, null);
			Assert.AreEqual(200, metadata.HttpResponseStatusCode);

			metadata.SetHttpResponseStatusCode(400, null);
			Assert.AreEqual(400, metadata.HttpResponseStatusCode);
			var immutableMetadata = metadata.ConvertToImmutableMetadata();
			Assert.AreEqual(400, immutableMetadata.HttpResponseStatusCode);
			Assert.IsNull(immutableMetadata.HttpResponseSubStatusCode);

			metadata.SetHttpResponseStatusCode(404, 420);
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

		[Test]
		public void AddRequestParameter_LastInWins()
		{
			var key = "testKey";
			var valueA = "valueA";
			var valueB = "valueB";

			var metadata = new TransactionMetadata();
			metadata.AddRequestParameter(key, valueA);
			metadata.AddRequestParameter(key, valueB);

			var immutableTransactionMetadata = metadata.ConvertToImmutableMetadata();

			var requestParameters = immutableTransactionMetadata.RequestParameters.ToDictionary();

			var result = requestParameters[key];

			Assert.AreEqual(result, valueB);
		}

		[Test]
		public void AddUserAttribute_LastInWins()
		{
			var key = "testKey";
			var valueA = "valueA";
			var valueB = "valueB";

			var metadata = new TransactionMetadata();
			metadata.AddUserAttribute(key, valueA);
			metadata.AddUserAttribute(key, valueB);

			var immutableTransactionMetadata = metadata.ConvertToImmutableMetadata();

			var userAttributes = immutableTransactionMetadata.UserAttributes.ToDictionary();

			var result = userAttributes[key];

			Assert.AreEqual(result, valueB);
		}

		[Test]
		public void AddUserErrorAttribute_LastInWins()
		{
			var key = "testKey";
			var valueA = "valueA";
			var valueB = "valueB";

			var transactionMetadata = new TransactionMetadata();
			transactionMetadata.AddUserErrorAttribute(key, valueA);
			transactionMetadata.AddUserErrorAttribute(key, valueB);

			var immutableTransactionMetadata = transactionMetadata.ConvertToImmutableMetadata();

			var userErrorAttributes = immutableTransactionMetadata.UserErrorAttributes.ToDictionary();

			var result = userErrorAttributes[key];

			Assert.AreEqual(result, valueB);
		}

		#region Distributed Trace

		[Test]
		public void Build_HasEmptyDistributedTracePropertiesIfNeverSet()
		{
			var transactionMetadata = new TransactionMetadata();
			var immutableMetadata = transactionMetadata.ConvertToImmutableMetadata();

			NrAssert.Multiple(
				() => Assert.That(transactionMetadata.DistributedTraceType, Is.Null),
				() => Assert.That(immutableMetadata.DistributedTraceType, Is.Null),
				() => Assert.That(transactionMetadata.DistributedTraceAppId, Is.Null),
				() => Assert.That(immutableMetadata.DistributedTraceAppId, Is.Null),
				() => Assert.That(transactionMetadata.DistributedTraceAccountId, Is.Null),
				() => Assert.That(immutableMetadata.DistributedTraceAccountId, Is.Null),
				() => Assert.That(transactionMetadata.DistributedTraceType, Is.Null),
				() => Assert.That(immutableMetadata.DistributedTraceType, Is.Null),
				() => Assert.That(transactionMetadata.DistributedTraceTrustKey, Is.Null),
				() => Assert.That(immutableMetadata.DistributedTraceTrustKey, Is.Null),
				() => Assert.That(immutableMetadata.DistributedTraceTransactionId, Is.Null),
				() => Assert.That(transactionMetadata.DistributedTraceGuid, Is.Null),
				() => Assert.That(immutableMetadata.DistributedTraceGuid, Is.Null),
				() => Assert.That(transactionMetadata.DistributedTraceSampled, Is.Null),
				() => Assert.That(transactionMetadata.DistributedTraceTraceId, Is.Null),
				() => Assert.That(immutableMetadata.DistributedTraceTraceId, Is.Null),
				() => Assert.That(transactionMetadata.DistributedTraceTransportType, Is.Null),
				() => Assert.That(immutableMetadata.DistributedTraceTransportType, Is.Null)
			);
		}

		[Test]
		public void ConvertToImmutableMetadata_SetsDistributedTraceProperties()
		{
			var transactionMetadata = new TransactionMetadata();

			transactionMetadata.DistributedTraceType = "type";
			transactionMetadata.DistributedTraceAccountId = "acct";
			transactionMetadata.DistributedTraceAppId = "app";
			transactionMetadata.DistributedTraceGuid = "id";
			transactionMetadata.DistributedTraceSampled = false;
			transactionMetadata.DistributedTraceTraceId = "trace";
			transactionMetadata.DistributedTraceTrustKey = "12345";
			transactionMetadata.SetDistributedTraceTransportType((TransportType)(-1));
			transactionMetadata.Priority = 0.6f;
			transactionMetadata.DistributedTraceTransactionId = "12345";

			var immutableMetadata = transactionMetadata.ConvertToImmutableMetadata();

			NrAssert.Multiple(
				() => Assert.AreEqual(transactionMetadata.DistributedTraceType, immutableMetadata.DistributedTraceType),
				() => Assert.AreEqual(transactionMetadata.DistributedTraceAccountId, immutableMetadata.DistributedTraceAccountId),
				() => Assert.AreEqual(transactionMetadata.DistributedTraceAppId, immutableMetadata.DistributedTraceAppId),
				() => Assert.AreEqual(transactionMetadata.DistributedTraceAppId, immutableMetadata.DistributedTraceAppId),
				() => Assert.AreEqual(transactionMetadata.DistributedTraceGuid, immutableMetadata.DistributedTraceGuid),
				() => Assert.AreEqual(transactionMetadata.DistributedTraceSampled, immutableMetadata.DistributedTraceSampled),
				() => Assert.AreEqual(transactionMetadata.DistributedTraceTraceId, immutableMetadata.DistributedTraceTraceId),
				() => Assert.AreEqual(transactionMetadata.DistributedTraceTrustKey, immutableMetadata.DistributedTraceTrustKey),
				() => Assert.AreEqual(transactionMetadata.DistributedTraceTransportType, immutableMetadata.DistributedTraceTransportType),
				() => Assert.AreEqual(transactionMetadata.Priority, immutableMetadata.Priority),
				() => Assert.AreEqual(transactionMetadata.DistributedTraceTransactionId, immutableMetadata.DistributedTraceTransactionId)
			);
		}

		[TestCase(TransportType.Unknown, "Unknown")]
		[TestCase(TransportType.HTTP, "HTTP")]
		[TestCase(TransportType.HTTPS, "HTTPS")]
		[TestCase(TransportType.Kafka, "Kafka")]
		[TestCase(TransportType.JMS, "JMS")]
		[TestCase(TransportType.IronMQ, "IronMQ")]
		[TestCase(TransportType.AMQP, "AMQP")]
		[TestCase(TransportType.Queue, "Queue")]
		[TestCase(TransportType.Other, "Other")]
		[TestCase((TransportType)(-1), "Unknown")]
		[TestCase((TransportType)99999, "Unknown")]
		public void ConvertToImmutableMetadata_DistributedTraceTransportType(TransportType transportType, string expectedTransportTypeName)
		{
			var transactionMetadata = new TransactionMetadata();
			transactionMetadata.SetDistributedTraceTransportType(transportType);

			var immutableMetadata = transactionMetadata.ConvertToImmutableMetadata();

			Assert.AreEqual(expectedTransportTypeName, immutableMetadata.DistributedTraceTransportType);
		}
		#endregion Distributed Trace
	}
}
