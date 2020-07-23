using System;
using System.Collections.Generic;
using Couchbase;
using Couchbase.Configuration.Client;
using Couchbase.Core;

namespace NewRelic.Agent.IntegrationTests.Shared.Couchbase
{
	public class CouchbaseConnection : IDisposable
	{
		private ICluster _cluster;
		public IBucket Bucket { get; private set; }

		public void Connect()
		{
			var config = GetConnectionConfig();
			_cluster = new Cluster(config);
			Bucket = _cluster.OpenBucket(CouchbaseTestObject.CouchbaseTestBucket);
		}

		public void Disconnect()
		{
			_cluster.CloseBucket(Bucket);
			Bucket.Dispose();
			Bucket = null;
			_cluster.Dispose();
			_cluster = null;
		}

		private ClientConfiguration GetConnectionConfig()
		{
			var config = new ClientConfiguration();
			config.Servers = new List<Uri>()
			{
				new Uri(CouchbaseTestObject.CouchbaseServerUrl)
			};
			config.UseSsl = false;
			return config;
		}

		public void Dispose()
		{
			Disconnect();
		}
	}
}
