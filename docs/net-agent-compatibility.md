<!-- GENERATED FILE — do not edit by hand.
     Source: build/CompatibilityDocs/compatibility.yaml
     Regenerate: dotnet run --project build/CompatibilityDocs -->

# .NET agent automatic instrumentation compatibility

## Contents
- [.NET Core](#net-core) — [App frameworks](#app-frameworks) · [Datastores](#datastores) · [External call libraries](#external-call-libraries) · [Large language model (LLM) libraries](#large-language-model-llm-libraries) · [Logging frameworks](#logging-frameworks) · [Message systems](#message-systems) · [Background jobs and workflows](#background-jobs-and-workflows)
- [.NET Framework](#net-framework) — [App frameworks](#app-frameworks-1) · [Datastores](#datastores-1) · [External call libraries](#external-call-libraries-1) · [Large language model (LLM) libraries](#large-language-model-llm-libraries-1) · [Logging frameworks](#logging-frameworks-1) · [Message systems](#message-systems-1) · [Background jobs and workflows](#background-jobs-and-workflows-1)

## .NET Core

### App frameworks

The .NET agent automatically instruments these application frameworks:

- ASP.NET Core MVC (includes Web API): 2.0, 2.1, 2.2, 3.0, 3.1, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0
- ASP.NET Core Razor Pages: 6.0, 7.0, 8.0, 9.0, 10.0 (min agent v10.19.0)

### Datastores

The .NET agent automatically instruments the performance of .NET application calls to these datastores:

| Library | NuGet package | Versions tested | Min agent version | Notes |
| --- | --- | --- | --- | --- |
| Cosmos DB | [Microsoft.Azure.Cosmos](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/) | 3.23.0 – 3.60.0 | 9.2.0 | <ul><li>Versions 3.35.0+ supported since agent v10.32.0.</li></ul> |
| Couchbase | [CouchbaseNetClient](https://www.nuget.org/packages/CouchbaseNetClient/) | 3.5.1 – 3.6.6 | — | <ul><li>Instance details aren't available for Couchbase.</li><li>Known incompatible versions: 3.0.x, 3.1.x.</li><li>With CouchbaseNetClient 2.x, the following methods are not instrumented by default in favor of their multi-document counterparts: <br>`Get(string key)` <br>`GetDocument(string key)` <br>`Remove(string key)` <br>`Remove(string key, ulong cas)` <br>`Upsert(string key, T value)`.</li><li>Versions 3.2.0+ supported since agent v10.40.0.</li></ul> |
| Microsoft SQL Server | [System.Data.SqlClient](https://www.nuget.org/packages/System.Data.SqlClient/) | 4.4.0 – 4.8.6 | — |  |
| Microsoft SQL Server | [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient/) | 5.2.2 – 7.0.1 | — |  |
| System.Data.ODBC | [System.Data.Odbc](https://www.nuget.org/packages/System.Data.Odbc/) | 8.0.0 – 10.0.8 | 10.35.0 | <ul><li>The following features supported for ODBC datastore calls in .NET Framework (using the built-in System.Data namespace) are not supported for .NET 8+: Connection `Open`/`OpenAsync` calls, SqlDataReader `Read`/`NextResult` calls, and slow SQL traces.</li></ul> |
| MongoDB (modern driver) | [MongoDB.Driver](https://www.nuget.org/packages/MongoDB.Driver/) | 2.8.1 – 3.9.0 | — | <ul><li>Versions 3.0.0+ supported since agent v10.40.0.</li><li>Beginning in agent version 10.12.0, the following methods added in or after driver version 2.7 are instrumented: <br>`IMongoCollection.CountDocuments[Async]` <br>`IMongoCollection.EstimatedDocumentCount[Async]` <br>`IMongoCollection.AggregateToCollection[Async]` <br>`IMongoDatabase.ListCollectionNames[Async]` <br>`IMongoDatabase.Aggregate[Async]` <br>`IMongoDatabase.AggregateToCollection[Async]` <br>`IMongoDatabase.Watch[Async]`.</li></ul> |
| MySQL | [MySql.Data](https://www.nuget.org/packages/MySql.Data/) | 8.0.28 – 9.7.0 | — | <ul><li>Versions 9.7.0+ supported since agent v10.52.0.</li></ul> |
| MySQL | [MySqlConnector](https://www.nuget.org/packages/MySqlConnector/) | 1.0.1 – 2.5.0 | — |  |
| Oracle | [Oracle.ManagedDataAccess.Core](https://www.nuget.org/packages/Oracle.ManagedDataAccess.Core/) | 23.4.0 – 23.26.200 | — | <ul><li>Older versions may be instrumented but are not tested or supported.</li></ul> |
| PostgreSQL | [Npgsql](https://www.nuget.org/packages/Npgsql/) | 4.1.13 – 7.0.7 | — | <ul><li>Prior versions of Npgsql may also be instrumented, but duplicate and/or missing metrics are possible.</li></ul> |
| StackExchange.Redis | [StackExchange.Redis](https://www.nuget.org/packages/StackExchange.Redis/) | 2.13.17 | — |  |
| Elasticsearch | [Elastic.Clients.Elasticsearch](https://www.nuget.org/packages/Elastic.Clients.Elasticsearch/) | 8.0.0 – 9.0.7 | — | <ul><li>Versions 8.10.0+ supported since agent v10.20.1.</li><li>Versions 8.12.1+ supported since agent v10.23.0.</li><li>Versions later than 8.15.10 are supported only when [OpenTelemetry API support](https://docs.newrelic.com/docs/apm/agents/manage-apm-agents/opentelemetry-api-support/) is enabled.</li></ul> |
| Elasticsearch | [NEST](https://www.nuget.org/packages/NEST/) | 7.0.0 – 7.17.5 | — |  |
| Elasticsearch | [Elasticsearch.Net](https://www.nuget.org/packages/Elasticsearch.Net/) | 7.0.0 – 7.17.5 | — |  |
| Memcached | [EnyimMemcachedCore](https://www.nuget.org/packages/EnyimMemcachedCore/) | 2.1.9 – 3.5.1 | — |  |
| DynamoDB | [AWSSDK.DynamoDBv2](https://www.nuget.org/packages/AWSSDK.DynamoDBv2/) | 4.0.18.6 | 10.33.0 |  |

The .NET agent doesn't collect data about datastore server processes. It only collects data from datastore client library usage. To directly monitor datastore server processes, use the New Relic infrastructure agent with [on-host integrations](https://docs.newrelic.com/docs/infrastructure/host-integrations/get-started/introduction-host-integrations/).

By default, the .NET agent doesn't capture SQL parameters for stored procedures or parameterized queries in a query trace. To see these SQL parameters, you must enable the collection feature in the [agent configuration](https://docs.newrelic.com/docs/apm/agents/net-agent/configuration/net-agent-configuration/#datastore-tracer-query-parameters).

All datastores listed above provide [instance details](https://docs.newrelic.com/docs/apm/applications-menu/features/analyze-database-instance-level-performance-issues) except where noted, and collection is enabled by default. To request instance-level information from datastores not currently listed, get support at [support.newrelic.com](https://support.newrelic.com).

If your datastore isn't listed here, you can add custom instrumentation using the `RecordDatastoreSegment` method in the [.NET agent API](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#ITransaction).

### External call libraries

The .NET agent automatically instruments these external call libraries:

| Library | NuGet package | Versions tested | Min agent version | Notes |
| --- | --- | --- | --- | --- |
| HttpClient | — | — | — | <details><summary>Instrumented methods (8)</summary><ul><li><code>SendAsync</code></li><li><code>GetAsync</code></li><li><code>PostAsync</code></li><li><code>PutAsync</code></li><li><code>DeleteAsync</code></li><li><code>GetStringAsync</code></li><li><code>GetStreamAsync</code></li><li><code>GetByteArrayAsync</code></li></ul></details> |

### Large language model (LLM) libraries

The .NET agent [can be configured](https://docs.newrelic.com/docs/apm/agents/net-agent/configuration/net-agent-configuration/#ai_monitoring) to automatically instrument these LLM frameworks:

| Library | NuGet package | Versions tested | Min agent version | Notes |
| --- | --- | --- | --- | --- |
| AWS Bedrock | [AWSSDK.BedrockRuntime](https://www.nuget.org/packages/AWSSDK.BedrockRuntime/) | 3.7.301.35 – 4.0.20.1 | — | <ul><li>Instrumented since agent v10.23.0 (`InvokeModelAsync`); v10.37.0 adds `ConverseAsync`.</li></ul> |
| OpenAI | [OpenAI](https://www.nuget.org/packages/OpenAI/) | 2.0.0 – 2.8.0 | 10.37.0 | <ul><li>`CompleteChat` / `CompleteChatAsync` — only Text completions are supported.</li></ul> |
| Azure OpenAI | [Azure.AI.OpenAI](https://www.nuget.org/packages/Azure.AI.OpenAI/) | 2.0.0 – 2.8.0-beta.1 | 10.37.0 | <ul><li>`CompleteChat` / `CompleteChatAsync` — only Text completions are supported.</li></ul> |

### Logging frameworks

The .NET agent [can be configured](https://docs.newrelic.com/docs/apm/agents/net-agent/configuration/net-agent-configuration/#application_logging) to automatically instrument these logging frameworks for automatic logs-in-context with [agent forwarding](https://docs.newrelic.com/docs/logs/logs-context/net-configure-logs-context-all/#1-agent) and [local log decoration](https://docs.newrelic.com/docs/logs/logs-context/net-configure-logs-context-all/#2-decorate):

| Library | NuGet package | Versions tested | Min agent version | Notes |
| --- | --- | --- | --- | --- |
| Log4Net | [log4net](https://www.nuget.org/packages/log4net/) | 2.0.10 – 3.3.1 | 9.7.0 |  |
| Serilog | [Serilog](https://www.nuget.org/packages/Serilog/) | 2.8.0 – 4.3.1 | 9.7.0 |  |
| NLog | [NLog](https://www.nuget.org/packages/NLog/) | 4.5.9 – 6.1.3 | 9.7.0 |  |
| Microsoft.Extensions.Logging | [Microsoft.Extensions.Logging](https://www.nuget.org/packages/Microsoft.Extensions.Logging/) | 3.0.3 – 10.0.8 | 10.0.0 | <ul><li>On .NET Framework, supported beginning with agent v9.7.0.</li></ul> |

### Message systems

The agent automatically instruments these message systems:

| Library | NuGet package | Versions tested | Min agent version | Notes |
| --- | --- | --- | --- | --- |
| Confluent.Kafka | [Confluent.Kafka](https://www.nuget.org/packages/Confluent.Kafka/) | 2.14.0 | — | <ul><li>Produce and consume on topics.</li></ul><details><summary>Instrumented methods (3)</summary><ul><li><code>IProducer.Produce</code></li><li><code>IProducer.ProduceAsync</code></li><li><code>IConsumer.Consume</code></li></ul></details> |
| NServiceBus | [NServiceBus](https://www.nuget.org/packages/NServiceBus/) | 8.2.0 – 10.2.4 | — | <ul><li>Puts and takes on messages.</li></ul> |
| RabbitMQ | [RabbitMQ.Client](https://www.nuget.org/packages/RabbitMQ.Client/) | 5.2.0 – 7.1.2 | — | <ul><li>Puts and takes on messages and queue purge.</li><li>When receiving messages using an `IBasicConsumer`, the `EventingBasicConsumer` is the only implementation that is instrumented.</li><li>`BasicGet` is instrumented, but the agent does not support [distributed tracing](https://docs.newrelic.com/docs/apm/distributed-tracing/getting-started/introduction-distributed-tracing/) for `BasicGet`.</li><li>Versions later than 6.8.1 are supported only when [OpenTelemetry API support](https://docs.newrelic.com/docs/apm/agents/manage-apm-agents/opentelemetry-api-support/) is enabled.</li></ul><details><summary>Instrumented methods (5)</summary><ul><li><code>IModel.BasicGet</code></li><li><code>IModel.BasicPublish</code></li><li><code>IModel.BasicConsume</code></li><li><code>IModel.QueuePurge</code></li><li><code>EventingBasicConsumer.HandleBasicDeliver</code></li></ul></details> |
| MassTransit | [MassTransit](https://www.nuget.org/packages/MassTransit/) | 7.3.1 – 8.5.7 | 10.19.0 | <ul><li>Message publish/send and consume.</li></ul> |
| AWSSDK.SQS | [AWSSDK.SQS](https://www.nuget.org/packages/AWSSDK.SQS/) | 4.0.2.33 | 10.27.0 | <ul><li>Message send and receive.</li></ul> |
| AWSSDK.Kinesis | [AWSSDK.Kinesis](https://www.nuget.org/packages/AWSSDK.Kinesis/) | 4.0.8.19 | 10.40.0 | <ul><li>`PutRecord(s)Async` and `GetRecordsAsync` are instrumented as message broker operations; other operations as basic method calls.</li></ul> |
| AWSSDK.KinesisFirehose | [AWSSDK.KinesisFirehose](https://www.nuget.org/packages/AWSSDK.KinesisFirehose/) | 4.0.3.32 | 10.40.0 | <ul><li>All Kinesis Firehose operations are instrumented as basic method calls.</li></ul> |
| Azure Service Bus | [Azure.Messaging.ServiceBus](https://www.nuget.org/packages/Azure.Messaging.ServiceBus/) | 7.11.0 – 7.18.2 | 10.42.0 | <ul><li>Message send and receive.</li><li>Supports Processor mode.</li></ul><details><summary>Instrumented methods (13)</summary><ul><li><code>ReceiverManager.ProcessOneMessageWithinScopeAsync</code></li><li><code>ServiceBusProcessor.OnProcessMessageAsync</code></li><li><code>ServiceBusSender.SendMessagesAsync</code></li><li><code>ServiceBusSender.ScheduleMessagesAsync</code></li><li><code>ServiceBusSender.CancelScheduledMessagesAsync</code></li><li><code>ServiceBusReceiver.ReceiveMessagesAsync</code></li><li><code>ServiceBusReceiver.ReceiveDeferredMessagesAsync</code></li><li><code>ServiceBusReceiver.PeekMessagesInternalAsync</code></li><li><code>ServiceBusReceiver.CompleteMessageAsync</code></li><li><code>ServiceBusReceiver.AbandonMessageAsync</code></li><li><code>ServiceBusReceiver.DeadLetterInternalAsync</code></li><li><code>ServiceBusReceiver.DeferMessageAsync</code></li><li><code>ServiceBusReceiver.RenewMessageLockAsync</code></li></ul></details> |

### Background jobs and workflows

The .NET agent automatically instruments the performance of .NET application calls to these background job and workflow libraries:

| Library | NuGet package | Versions tested | Min agent version | Notes |
| --- | --- | --- | --- | --- |
| Hangfire | [Hangfire](https://www.nuget.org/packages/Hangfire/) | 1.7.15 – 1.8.23 | 10.51.0 |  |

## .NET Framework

### App frameworks

The .NET agent automatically instruments these application frameworks:

- ASP.NET MVC: 2, 3, 4, 5
- ASP.NET Web API: 2
- ASP.NET Core MVC (on .NET Framework): 2.0, 2.1, 2.2
- ASP.NET Web Forms: (all supported .NET Framework versions)
- OWIN-hosted Web API: Microsoft.Owin.Host.HttpListener 2.x, 3.x, 4.x
- SOAP-based web services: (all supported .NET Framework versions)
- [WCF](https://docs.newrelic.com/docs/apm/agents/net-agent/other-installation/install-net-agent-windows-communication-foundation-wcf/): (all supported .NET Framework versions)
- Castle MonoRail: v2 (no longer supported in agent 10.0 or higher)

### Datastores

The .NET agent automatically instruments the performance of .NET application calls to these datastores:

- IBM DB2: (built-in driver)

| Library | NuGet package | Versions tested | Min agent version | Notes |
| --- | --- | --- | --- | --- |
| Cosmos DB | [Microsoft.Azure.Cosmos](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/) | 3.23.0 – 3.60.0 | 9.2.0 | <ul><li>Versions 3.35.0+ supported since agent v10.32.0.</li></ul> |
| Couchbase | [CouchbaseNetClient](https://www.nuget.org/packages/CouchbaseNetClient/) | 2.7.27 – 3.6.6 | — | <ul><li>Instance details aren't available for Couchbase.</li><li>Known incompatible versions: 3.0.x, 3.1.x.</li><li>With CouchbaseNetClient 2.x, the following methods are not instrumented by default in favor of their multi-document counterparts: <br>`Get(string key)` <br>`GetDocument(string key)` <br>`Remove(string key)` <br>`Remove(string key, ulong cas)` <br>`Upsert(string key, T value)`.</li><li>Versions 3.2.0+ supported since agent v10.40.0.</li></ul> |
| Microsoft SQL Server | [System.Data.SqlClient](https://www.nuget.org/packages/System.Data.SqlClient/) | 4.4.0 – 4.8.6 | — |  |
| Microsoft SQL Server | [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient/) | 1.0.19239.1 – 7.0.1 | — |  |
| Microsoft SQL Server | System.Data | 4.6.2 – 4.8 | — | <ul><li>Built-in .NET Framework assembly; no NuGet package required.</li></ul> |
| System.Data.ODBC | [System.Data.Odbc](https://www.nuget.org/packages/System.Data.Odbc/) | 8.0.0 – 10.0.8 | 10.35.0 | <ul><li>The following features supported for ODBC datastore calls in .NET Framework (using the built-in System.Data namespace) are not supported for .NET 8+: Connection `Open`/`OpenAsync` calls, SqlDataReader `Read`/`NextResult` calls, and slow SQL traces.</li></ul> |
| MongoDB (legacy driver) | [mongocsharpdriver](https://www.nuget.org/packages/mongocsharpdriver/) | 1.10.0 | — | <ul><li>Known incompatible versions: Instance details aren't available in version 2 and lower.</li></ul> |
| MongoDB (modern driver) | [MongoDB.Driver](https://www.nuget.org/packages/MongoDB.Driver/) | 2.3.0 – 3.9.0 | — | <ul><li>Versions 3.0.0+ supported since agent v10.40.0.</li><li>Beginning in agent version 10.12.0, the following methods added in or after driver version 2.7 are instrumented: <br>`IMongoCollection.CountDocuments[Async]` <br>`IMongoCollection.EstimatedDocumentCount[Async]` <br>`IMongoCollection.AggregateToCollection[Async]` <br>`IMongoDatabase.ListCollectionNames[Async]` <br>`IMongoDatabase.Aggregate[Async]` <br>`IMongoDatabase.AggregateToCollection[Async]` <br>`IMongoDatabase.Watch[Async]`.</li></ul> |
| MySQL | [MySql.Data](https://www.nuget.org/packages/MySql.Data/) | 8.0.28 – 9.7.0 | — | <ul><li>Versions 9.7.0+ supported since agent v10.52.0.</li></ul> |
| MySQL | [MySqlConnector](https://www.nuget.org/packages/MySqlConnector/) | 1.0.1 – 2.5.0 | — |  |
| Oracle | [Oracle.ManagedDataAccess](https://www.nuget.org/packages/Oracle.ManagedDataAccess/) | 12.1.2400 – 23.26.200 | — | <ul><li>Older versions may be instrumented but are not tested or supported.</li></ul> |
| PostgreSQL | [Npgsql](https://www.nuget.org/packages/Npgsql/) | 4.0.14 – 7.0.7 | — | <ul><li>Prior versions of Npgsql may also be instrumented, but duplicate and/or missing metrics are possible.</li></ul> |
| ServiceStack.Redis | [ServiceStack.Redis](https://www.nuget.org/packages/ServiceStack.Redis/) | 4.0.40 | — | <ul><li>Known incompatible versions: 4.0.44 or higher.</li></ul> |
| StackExchange.Redis | [StackExchange.Redis](https://www.nuget.org/packages/StackExchange.Redis/) | 2.0.601 – 2.13.17 | — |  |
| Elasticsearch | [Elastic.Clients.Elasticsearch](https://www.nuget.org/packages/Elastic.Clients.Elasticsearch/) | 8.0.0 – 8.18.3 | — | <ul><li>Versions 8.10.0+ supported since agent v10.20.1.</li><li>Versions 8.12.1+ supported since agent v10.23.0.</li><li>Versions later than 8.15.10 are supported only when [OpenTelemetry API support](https://docs.newrelic.com/docs/apm/agents/manage-apm-agents/opentelemetry-api-support/) is enabled.</li></ul> |
| Elasticsearch | [NEST](https://www.nuget.org/packages/NEST/) | 7.0.0 – 7.17.5 | — |  |
| Elasticsearch | [Elasticsearch.Net](https://www.nuget.org/packages/Elasticsearch.Net/) | 7.0.0 – 7.17.5 | — |  |
| Memcached | [EnyimMemcachedCore](https://www.nuget.org/packages/EnyimMemcachedCore/) | — | — |  |
| DynamoDB | [AWSSDK.DynamoDBv2](https://www.nuget.org/packages/AWSSDK.DynamoDBv2/) | 4.0.18.6 | 10.33.0 |  |

The .NET agent doesn't collect data about datastore server processes. It only collects data from datastore client library usage. To directly monitor datastore server processes, use the New Relic infrastructure agent with [on-host integrations](https://docs.newrelic.com/docs/infrastructure/host-integrations/get-started/introduction-host-integrations/).

By default, the .NET agent doesn't capture SQL parameters for stored procedures or parameterized queries in a query trace. To see these SQL parameters, you must enable the collection feature in the [agent configuration](https://docs.newrelic.com/docs/apm/agents/net-agent/configuration/net-agent-configuration/#datastore-tracer-query-parameters).

All datastores listed above provide [instance details](https://docs.newrelic.com/docs/apm/applications-menu/features/analyze-database-instance-level-performance-issues) except where noted, and collection is enabled by default. To request instance-level information from datastores not currently listed, get support at [support.newrelic.com](https://support.newrelic.com).

If your datastore isn't listed here, you can add custom instrumentation using the `RecordDatastoreSegment` method in the [.NET agent API](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#ITransaction).

### External call libraries

The .NET agent automatically instruments these external call libraries:

| Library | NuGet package | Versions tested | Min agent version | Notes |
| --- | --- | --- | --- | --- |
| HttpClient | — | — | — | <details><summary>Instrumented methods (8)</summary><ul><li><code>SendAsync</code></li><li><code>GetAsync</code></li><li><code>PostAsync</code></li><li><code>PutAsync</code></li><li><code>DeleteAsync</code></li><li><code>GetStringAsync</code></li><li><code>GetStreamAsync</code></li><li><code>GetByteArrayAsync</code></li></ul></details> |
| RestSharp | [RestSharp](https://www.nuget.org/packages/RestSharp/) | 105.2.3 – 114.0.0 | — | <ul><li>Known incompatible versions: 106.8.0, 106.9.0, 106.10.0, 106.10.1.</li></ul><details><summary>Instrumented methods (7)</summary><ul><li><code>ExecuteTaskAsync</code></li><li><code>ExecuteGetTaskAsync</code></li><li><code>ExecutePostTaskAsync</code></li><li><code>Execute</code></li><li><code>ExecuteAsGet</code></li><li><code>ExecuteAsPost</code></li><li><code>DownloadData</code></li></ul></details> |
| HttpWebRequest | — | — | — | <details><summary>Instrumented methods (1)</summary><ul><li><code>GetResponse</code></li></ul></details> |

### Large language model (LLM) libraries

The .NET agent [can be configured](https://docs.newrelic.com/docs/apm/agents/net-agent/configuration/net-agent-configuration/#ai_monitoring) to automatically instrument these LLM frameworks:

| Library | NuGet package | Versions tested | Min agent version | Notes |
| --- | --- | --- | --- | --- |
| AWS Bedrock | [AWSSDK.BedrockRuntime](https://www.nuget.org/packages/AWSSDK.BedrockRuntime/) | 3.7.301.35 – 4.0.20.1 | — | <ul><li>Instrumented since agent v10.23.0 (`InvokeModelAsync`); v10.37.0 adds `ConverseAsync`.</li></ul> |
| OpenAI | [OpenAI](https://www.nuget.org/packages/OpenAI/) | 2.0.0 – 2.8.0 | 10.37.0 | <ul><li>`CompleteChat` / `CompleteChatAsync` — only Text completions are supported.</li></ul> |
| Azure OpenAI | [Azure.AI.OpenAI](https://www.nuget.org/packages/Azure.AI.OpenAI/) | 2.0.0 – 2.8.0-beta.1 | 10.37.0 | <ul><li>`CompleteChat` / `CompleteChatAsync` — only Text completions are supported.</li></ul> |

### Logging frameworks

The .NET agent [can be configured](https://docs.newrelic.com/docs/apm/agents/net-agent/configuration/net-agent-configuration/#application_logging) to automatically instrument these logging frameworks for automatic logs-in-context with [agent forwarding](https://docs.newrelic.com/docs/logs/logs-context/net-configure-logs-context-all/#1-agent) and [local log decoration](https://docs.newrelic.com/docs/logs/logs-context/net-configure-logs-context-all/#2-decorate):

| Library | NuGet package | Versions tested | Min agent version | Notes |
| --- | --- | --- | --- | --- |
| Log4Net | [log4net](https://www.nuget.org/packages/log4net/) | 1.2.10 – 3.3.1 | 9.7.0 |  |
| Serilog | [Serilog](https://www.nuget.org/packages/Serilog/) | 1.5.14 – 4.3.1 | 9.7.0 |  |
| NLog | [NLog](https://www.nuget.org/packages/NLog/) | 4.1.2 – 6.1.3 | 9.7.0 |  |
| Microsoft.Extensions.Logging | [Microsoft.Extensions.Logging](https://www.nuget.org/packages/Microsoft.Extensions.Logging/) | 3.0.0 – 10.0.8 | 10.0.0 | <ul><li>On .NET Framework, supported beginning with agent v9.7.0.</li></ul> |

### Message systems

The agent automatically instruments these message systems:

| Library | NuGet package | Versions tested | Min agent version | Notes |
| --- | --- | --- | --- | --- |
| Confluent.Kafka | [Confluent.Kafka](https://www.nuget.org/packages/Confluent.Kafka/) | — | — | <ul><li>Produce and consume on topics.</li></ul><details><summary>Instrumented methods (3)</summary><ul><li><code>IProducer.Produce</code></li><li><code>IProducer.ProduceAsync</code></li><li><code>IConsumer.Consume</code></li></ul></details> |
| MSMQ | — | — | — | <details><summary>Instrumented methods (4)</summary><ul><li><code>Message.Send</code></li><li><code>Message.Receive</code></li><li><code>Queue.Peek</code></li><li><code>Queue.Purge</code></li></ul></details> |
| NServiceBus | [NServiceBus](https://www.nuget.org/packages/NServiceBus/) | 5.0.0 – 8.2.4 | — | <ul><li>Puts and takes on messages.</li></ul> |
| RabbitMQ | [RabbitMQ.Client](https://www.nuget.org/packages/RabbitMQ.Client/) | 3.6.9 – 6.8.1 | — | <ul><li>Puts and takes on messages and queue purge.</li><li>When receiving messages using an `IBasicConsumer`, the `EventingBasicConsumer` is the only implementation that is instrumented.</li><li>`BasicGet` is instrumented, but the agent does not support [distributed tracing](https://docs.newrelic.com/docs/apm/distributed-tracing/getting-started/introduction-distributed-tracing/) for `BasicGet`.</li><li>Versions later than 6.8.1 are supported only when [OpenTelemetry API support](https://docs.newrelic.com/docs/apm/agents/manage-apm-agents/opentelemetry-api-support/) is enabled.</li></ul><details><summary>Instrumented methods (5)</summary><ul><li><code>IModel.BasicGet</code></li><li><code>IModel.BasicPublish</code></li><li><code>IModel.BasicConsume</code></li><li><code>IModel.QueuePurge</code></li><li><code>EventingBasicConsumer.HandleBasicDeliver</code></li></ul></details> |
| MassTransit | [MassTransit](https://www.nuget.org/packages/MassTransit/) | 7.1.0 – 8.5.7 | 10.19.0 | <ul><li>Message publish/send and consume.</li></ul> |
| AWSSDK.SQS | [AWSSDK.SQS](https://www.nuget.org/packages/AWSSDK.SQS/) | 4.0.2.33 | 10.27.0 | <ul><li>Message send and receive.</li></ul> |
| AWSSDK.Kinesis | [AWSSDK.Kinesis](https://www.nuget.org/packages/AWSSDK.Kinesis/) | 4.0.8.19 | 10.40.0 | <ul><li>`PutRecord(s)Async` and `GetRecordsAsync` are instrumented as message broker operations; other operations as basic method calls.</li></ul> |
| AWSSDK.KinesisFirehose | [AWSSDK.KinesisFirehose](https://www.nuget.org/packages/AWSSDK.KinesisFirehose/) | 4.0.3.32 | 10.40.0 | <ul><li>All Kinesis Firehose operations are instrumented as basic method calls.</li></ul> |
| Azure Service Bus | [Azure.Messaging.ServiceBus](https://www.nuget.org/packages/Azure.Messaging.ServiceBus/) | 7.11.0 – 7.18.2 | 10.42.0 | <ul><li>Message send and receive.</li><li>Supports Processor mode.</li></ul><details><summary>Instrumented methods (13)</summary><ul><li><code>ReceiverManager.ProcessOneMessageWithinScopeAsync</code></li><li><code>ServiceBusProcessor.OnProcessMessageAsync</code></li><li><code>ServiceBusSender.SendMessagesAsync</code></li><li><code>ServiceBusSender.ScheduleMessagesAsync</code></li><li><code>ServiceBusSender.CancelScheduledMessagesAsync</code></li><li><code>ServiceBusReceiver.ReceiveMessagesAsync</code></li><li><code>ServiceBusReceiver.ReceiveDeferredMessagesAsync</code></li><li><code>ServiceBusReceiver.PeekMessagesInternalAsync</code></li><li><code>ServiceBusReceiver.CompleteMessageAsync</code></li><li><code>ServiceBusReceiver.AbandonMessageAsync</code></li><li><code>ServiceBusReceiver.DeadLetterInternalAsync</code></li><li><code>ServiceBusReceiver.DeferMessageAsync</code></li><li><code>ServiceBusReceiver.RenewMessageLockAsync</code></li></ul></details> |

### Background jobs and workflows

The .NET agent automatically instruments the performance of .NET application calls to these background job and workflow libraries:

| Library | NuGet package | Versions tested | Min agent version | Notes |
| --- | --- | --- | --- | --- |
| Hangfire | [Hangfire](https://www.nuget.org/packages/Hangfire/) | 1.7.15 – 1.8.23 | 10.51.0 |  |
