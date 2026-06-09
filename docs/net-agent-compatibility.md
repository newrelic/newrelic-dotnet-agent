<!-- GENERATED FILE — do not edit by hand.
     Source: build/CompatibilityDocs/compatibility.yaml
     Regenerate: dotnet run --project build/CompatibilityDocs -->

# .NET agent automatic instrumentation compatibility

## .NET Core

### App frameworks

- ASP.NET Core MVC (includes Web API): 2.0, 2.1, 2.2, 3.0, 3.1, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0
- ASP.NET Core Razor Pages: 6.0, 7.0, 8.0, 9.0, 10.0 (min agent v10.19.0)

### Datastores

The .NET agent automatically instruments the performance of .NET application calls to these datastores:

| Library | NuGet package | Minimum version | Latest verified | Min agent version | Notes |
| --- | --- | --- | --- | --- | --- |
| Cosmos DB | [Microsoft.Azure.Cosmos](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/) | 3.23.0 | 3.60.0 | — | Versions 3.35.0+ supported since agent v10.32.0. |
| Couchbase | [CouchbaseNetClient](https://www.nuget.org/packages/CouchbaseNetClient/) | 3.5.1 | 3.6.6 | — | Known incompatible versions: 3.0.x, 3.1.x.<br>With CouchbaseNetClient 2.x, the following methods are not instrumented by default in favor of their multi-document counterparts: <br>`Get(string key)` <br>`GetDocument(string key)` <br>`Remove(string key)` <br>`Remove(string key, ulong cas)` <br>`Upsert(string key, T value)`.<br>Versions 3.2.0+ supported since agent v10.40.0. |
| Microsoft SQL Server | [System.Data.SqlClient](https://www.nuget.org/packages/System.Data.SqlClient/) | 4.4.0 | 4.8.6 | — |  |
| Microsoft SQL Server | [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient/) | 5.2.2 | 7.0.1 | — |  |
| System.Data.ODBC | [System.Data.Odbc](https://www.nuget.org/packages/System.Data.Odbc/) | 8.0.0 | 10.0.8 | 10.35.0 | The following features supported for ODBC datastore calls in .NET Framework (using the built-in System.Data namespace) are not supported for .NET 8+: Connection Open/OpenAsync calls, SqlDataReader Read/NextResult calls, and slow SQL traces. |
| MongoDB (modern driver) | [MongoDB.Driver](https://www.nuget.org/packages/MongoDB.Driver/) | 2.8.1 | 3.9.0 | — | Versions 3.0.0+ supported since agent v10.40.0.<br>Beginning in agent version 10.12.0, the following methods added in or after driver version 2.7 are instrumented: <br>`IMongoCollection.CountDocuments[Async]` <br>`IMongoCollection.EstimatedDocumentCount[Async]` <br>`IMongoCollection.AggregateToCollection[Async]` <br>`IMongoDatabase.ListCollectionNames[Async]` <br>`IMongoDatabase.Aggregate[Async]` <br>`IMongoDatabase.AggregateToCollection[Async]` <br>`IMongoDatabase.Watch[Async]`. |
| MySQL | [MySql.Data](https://www.nuget.org/packages/MySql.Data/) | 8.0.28 | 9.7.0 | — | Versions 9.7.0+ supported since agent v10.52.0. |
| MySQL | [MySqlConnector](https://www.nuget.org/packages/MySqlConnector/) | 1.0.1 | 2.5.0 | — |  |
| Oracle | [Oracle.ManagedDataAccess.Core](https://www.nuget.org/packages/Oracle.ManagedDataAccess.Core/) | 23.4.0 | 23.26.200 | — | Older versions may be instrumented but are not tested or supported. |
| PostgreSQL | [Npgsql](https://www.nuget.org/packages/Npgsql/) | 4.1.13 | 7.0.7 | — | Prior versions of Npgsql may also be instrumented, but duplicate and/or missing metrics are possible. |
| StackExchange.Redis | [StackExchange.Redis](https://www.nuget.org/packages/StackExchange.Redis/) | 2.13.17 | 2.13.17 | — |  |
| Elasticsearch | [Elastic.Clients.Elasticsearch](https://www.nuget.org/packages/Elastic.Clients.Elasticsearch/) | 8.0.0 | 9.0.7 | — | Versions 8.10.0+ supported since agent v10.20.1.<br>Versions 8.12.1+ supported since agent v10.23.0.<br>Versions later than 8.15.10 are supported only when [OpenTelemetry API support](https://docs.newrelic.com/docs/apm/agents/manage-apm-agents/opentelemetry-api-support/) is enabled. |
| Elasticsearch | [NEST](https://www.nuget.org/packages/NEST/) | 7.0.0 | 7.17.5 | — |  |
| Elasticsearch | [Elasticsearch.Net](https://www.nuget.org/packages/Elasticsearch.Net/) | 7.0.0 | 7.17.5 | — |  |
| Memcached | [EnyimMemcachedCore](https://www.nuget.org/packages/EnyimMemcachedCore/) | 2.1.9 | 3.5.1 | — |  |
| DynamoDB | [AWSSDK.DynamoDBv2](https://www.nuget.org/packages/AWSSDK.DynamoDBv2/) | 4.0.18.6 | 4.0.18.6 | 10.33.0 |  |

The .NET agent doesn't collect data about datastore server processes. It only collects data from datastore client library usage. To directly monitor datastore server processes, use the New Relic infrastructure agent with on-host integrations.

By default, the .NET agent doesn't capture SQL parameters for stored procedures or parameterized queries in a query trace. To see these SQL parameters, you must enable the collection feature in the agent configuration.

### External call libraries

The .NET agent automatically instruments these external call libraries:

| Library | NuGet package | Minimum version | Latest verified | Min agent version | Notes |
| --- | --- | --- | --- | --- | --- |
| HttpClient | — | — | — | — | Instruments:<br>`SendAsync`<br>`GetAsync`<br>`PostAsync`<br>`PutAsync`<br>`DeleteAsync`<br>`GetStringAsync`<br>`GetStreamAsync`<br>`GetByteArrayAsync` |

### Large language model (LLM) libraries

| Library | NuGet package | Minimum version | Latest verified | Min agent version | Notes |
| --- | --- | --- | --- | --- | --- |
| AWS Bedrock | [AWSSDK.BedrockRuntime](https://www.nuget.org/packages/AWSSDK.BedrockRuntime/) | 3.7.301.35 | 4.0.20.1 | — | Instrumented since agent v10.23.0 (InvokeModelAsync); v10.37.0 adds ConverseAsync. |
| OpenAI | [OpenAI](https://www.nuget.org/packages/OpenAI/) | 2.0.0 | 2.8.0 | 10.37.0 | CompleteChat / CompleteChatAsync — only Text completions are supported. |
| Azure OpenAI | [Azure.AI.OpenAI](https://www.nuget.org/packages/Azure.AI.OpenAI/) | 2.0.0 | 2.8.0-beta.1 | 10.37.0 | CompleteChat / CompleteChatAsync — only Text completions are supported. |

### Logging frameworks

| Library | NuGet package | Minimum version | Latest verified | Min agent version | Notes |
| --- | --- | --- | --- | --- | --- |
| Log4Net | [log4net](https://www.nuget.org/packages/log4net/) | 2.0.10 | 3.3.1 | 9.7.0 |  |
| Serilog | [Serilog](https://www.nuget.org/packages/Serilog/) | 2.8.0 | 4.3.1 | 9.7.0 |  |
| NLog | [NLog](https://www.nuget.org/packages/NLog/) | 4.5.9 | 6.1.3 | 9.7.0 |  |
| Microsoft.Extensions.Logging | [Microsoft.Extensions.Logging](https://www.nuget.org/packages/Microsoft.Extensions.Logging/) | 3.0.3 | 10.0.8 | 9.7.0 |  |

### Message systems

The agent automatically instruments these message systems:

| Library | NuGet package | Minimum version | Latest verified | Min agent version | Notes |
| --- | --- | --- | --- | --- | --- |
| Confluent.Kafka | [Confluent.Kafka](https://www.nuget.org/packages/Confluent.Kafka/) | 2.14.0 | 2.14.0 | — | Instruments:<br>`IProducer.Produce`<br>`IProducer.ProduceAsync`<br>`IConsumer.Consume` |
| NServiceBus | [NServiceBus](https://www.nuget.org/packages/NServiceBus/) | 8.2.0 | 10.2.4 | — |  |
| RabbitMQ | [RabbitMQ.Client](https://www.nuget.org/packages/RabbitMQ.Client/) | 5.2.0 | 7.1.2 | — | BasicGet is instrumented, but distributed tracing is not supported for BasicGet. Only EventingBasicConsumer is instrumented for IBasicConsumer.<br>Versions later than 6.8.1 are supported only when [OpenTelemetry API support](https://docs.newrelic.com/docs/apm/agents/manage-apm-agents/opentelemetry-api-support/) is enabled.<br>Instruments:<br>`IModel.BasicGet`<br>`IModel.BasicPublish`<br>`IModel.BasicConsume`<br>`IModel.QueuePurge`<br>`EventingBasicConsumer.HandleBasicDeliver` |
| MassTransit | [MassTransit](https://www.nuget.org/packages/MassTransit/) | 7.3.1 | 8.5.7 | 10.19.0 |  |
| AWSSDK.SQS | [AWSSDK.SQS](https://www.nuget.org/packages/AWSSDK.SQS/) | 4.0.2.33 | 4.0.2.33 | 10.27.0 |  |
| AWSSDK.Kinesis | [AWSSDK.Kinesis](https://www.nuget.org/packages/AWSSDK.Kinesis/) | 4.0.8.19 | 4.0.8.19 | 10.40.0 | PutRecord(s)Async and GetRecordsAsync are instrumented as message broker operations; other operations as basic method calls. |
| AWSSDK.KinesisFirehose | [AWSSDK.KinesisFirehose](https://www.nuget.org/packages/AWSSDK.KinesisFirehose/) | 4.0.3.32 | 4.0.3.32 | 10.40.0 | All Kinesis Firehose operations are instrumented as basic method calls. |
| Azure Service Bus | [Azure.Messaging.ServiceBus](https://www.nuget.org/packages/Azure.Messaging.ServiceBus/) | 7.11.0 | 7.18.2 | 10.42.0 | Supports Processor mode.<br>Instruments:<br>`Azure.Messaging.ServiceBus.ReceiverManager.ProcessOneMessageWithinScopeAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusProcessor.OnProcessMessageAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusSender.SendMessagesAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusSender.ScheduleMessagesAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusSender.CancelScheduledMessagesAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusReceiver.ReceiveMessagesAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusReceiver.ReceiveDeferredMessagesAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusReceiver.PeekMessagesInternalAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusReceiver.CompleteMessageAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusReceiver.AbandonMessageAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusReceiver.DeadLetterInternalAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusReceiver.DeferMessageAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusReceiver.RenewMessageLockAsync` |

### Background jobs and workflows

| Library | NuGet package | Minimum version | Latest verified | Min agent version | Notes |
| --- | --- | --- | --- | --- | --- |
| Hangfire | [Hangfire](https://www.nuget.org/packages/Hangfire/) | 1.7.15 | 1.8.23 | 10.51.0 |  |

## .NET Framework

### App frameworks

- ASP.NET MVC: 2, 3, 4, 5
- ASP.NET Web API: 2
- ASP.NET Core MVC (on .NET Framework): 2.0, 2.1, 2.2
- ASP.NET Web Forms: (all supported .NET Framework versions)

### Datastores

The .NET agent automatically instruments the performance of .NET application calls to these datastores:

- IBM DB2: (built-in driver)

| Library | NuGet package | Minimum version | Latest verified | Min agent version | Notes |
| --- | --- | --- | --- | --- | --- |
| Cosmos DB | [Microsoft.Azure.Cosmos](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/) | 3.23.0 | 3.60.0 | — | Versions 3.35.0+ supported since agent v10.32.0. |
| Couchbase | [CouchbaseNetClient](https://www.nuget.org/packages/CouchbaseNetClient/) | 2.7.27 | 3.6.6 | — | Known incompatible versions: 3.0.x, 3.1.x.<br>With CouchbaseNetClient 2.x, the following methods are not instrumented by default in favor of their multi-document counterparts: <br>`Get(string key)` <br>`GetDocument(string key)` <br>`Remove(string key)` <br>`Remove(string key, ulong cas)` <br>`Upsert(string key, T value)`.<br>Versions 3.2.0+ supported since agent v10.40.0. |
| Microsoft SQL Server | [System.Data.SqlClient](https://www.nuget.org/packages/System.Data.SqlClient/) | 4.4.0 | 4.8.6 | — |  |
| Microsoft SQL Server | [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient/) | 1.0.19239.1 | 7.0.1 | — |  |
| Microsoft SQL Server | System.Data | 4.6.2 | 4.8 | — | Built-in .NET Framework assembly; no NuGet package required. |
| System.Data.ODBC | [System.Data.Odbc](https://www.nuget.org/packages/System.Data.Odbc/) | 8.0.0 | 10.0.8 | 10.35.0 | The following features supported for ODBC datastore calls in .NET Framework (using the built-in System.Data namespace) are not supported for .NET 8+: Connection Open/OpenAsync calls, SqlDataReader Read/NextResult calls, and slow SQL traces. |
| MongoDB (legacy driver) | [mongocsharpdriver](https://www.nuget.org/packages/mongocsharpdriver/) | 1.10.0 | 1.10.0 | — | Known incompatible versions: Instance details aren't available in version 2 and lower. |
| MongoDB (modern driver) | [MongoDB.Driver](https://www.nuget.org/packages/MongoDB.Driver/) | 2.3.0 | 3.9.0 | — | Versions 3.0.0+ supported since agent v10.40.0.<br>Beginning in agent version 10.12.0, the following methods added in or after driver version 2.7 are instrumented: <br>`IMongoCollection.CountDocuments[Async]` <br>`IMongoCollection.EstimatedDocumentCount[Async]` <br>`IMongoCollection.AggregateToCollection[Async]` <br>`IMongoDatabase.ListCollectionNames[Async]` <br>`IMongoDatabase.Aggregate[Async]` <br>`IMongoDatabase.AggregateToCollection[Async]` <br>`IMongoDatabase.Watch[Async]`. |
| MySQL | [MySql.Data](https://www.nuget.org/packages/MySql.Data/) | 8.0.28 | 9.7.0 | — | Versions 9.7.0+ supported since agent v10.52.0. |
| MySQL | [MySqlConnector](https://www.nuget.org/packages/MySqlConnector/) | 1.0.1 | 2.5.0 | — |  |
| Oracle | [Oracle.ManagedDataAccess](https://www.nuget.org/packages/Oracle.ManagedDataAccess/) | 12.1.2400 | 23.26.200 | — | Older versions may be instrumented but are not tested or supported. |
| PostgreSQL | [Npgsql](https://www.nuget.org/packages/Npgsql/) | 4.0.14 | 7.0.7 | — | Prior versions of Npgsql may also be instrumented, but duplicate and/or missing metrics are possible. |
| ServiceStack.Redis | [ServiceStack.Redis](https://www.nuget.org/packages/ServiceStack.Redis/) | 4.0.40 | 4.0.40 | — | Known incompatible versions: 4.0.44 or higher. |
| StackExchange.Redis | [StackExchange.Redis](https://www.nuget.org/packages/StackExchange.Redis/) | 2.0.601 | 2.13.17 | — |  |
| Elasticsearch | [Elastic.Clients.Elasticsearch](https://www.nuget.org/packages/Elastic.Clients.Elasticsearch/) | 8.0.0 | 8.18.3 | — | Versions 8.10.0+ supported since agent v10.20.1.<br>Versions 8.12.1+ supported since agent v10.23.0.<br>Versions later than 8.15.10 are supported only when [OpenTelemetry API support](https://docs.newrelic.com/docs/apm/agents/manage-apm-agents/opentelemetry-api-support/) is enabled. |
| Elasticsearch | [NEST](https://www.nuget.org/packages/NEST/) | 7.0.0 | 7.17.5 | — |  |
| Elasticsearch | [Elasticsearch.Net](https://www.nuget.org/packages/Elasticsearch.Net/) | 7.0.0 | 7.17.5 | — |  |
| Memcached | [EnyimMemcachedCore](https://www.nuget.org/packages/EnyimMemcachedCore/) | — | — | — |  |
| DynamoDB | [AWSSDK.DynamoDBv2](https://www.nuget.org/packages/AWSSDK.DynamoDBv2/) | 4.0.18.6 | 4.0.18.6 | 10.33.0 |  |

The .NET agent doesn't collect data about datastore server processes. It only collects data from datastore client library usage. To directly monitor datastore server processes, use the New Relic infrastructure agent with on-host integrations.

By default, the .NET agent doesn't capture SQL parameters for stored procedures or parameterized queries in a query trace. To see these SQL parameters, you must enable the collection feature in the agent configuration.

### External call libraries

The .NET agent automatically instruments these external call libraries:

| Library | NuGet package | Minimum version | Latest verified | Min agent version | Notes |
| --- | --- | --- | --- | --- | --- |
| HttpClient | — | — | — | — | Instruments:<br>`SendAsync`<br>`GetAsync`<br>`PostAsync`<br>`PutAsync`<br>`DeleteAsync`<br>`GetStringAsync`<br>`GetStreamAsync`<br>`GetByteArrayAsync` |
| RestSharp | [RestSharp](https://www.nuget.org/packages/RestSharp/) | 105.2.3 | 114.0.0 | — | Known incompatible versions: 106.8.0, 106.9.0, 106.10.0, 106.10.1.<br>Instruments:<br>`ExecuteTaskAsync`<br>`ExecuteGetTaskAsync`<br>`ExecutePostTaskAsync`<br>`Execute`<br>`ExecuteAsGet`<br>`ExecuteAsPost`<br>`DownloadData` |
| HttpWebRequest | — | — | — | — | Instruments:<br>`GetResponse` |

### Large language model (LLM) libraries

| Library | NuGet package | Minimum version | Latest verified | Min agent version | Notes |
| --- | --- | --- | --- | --- | --- |
| AWS Bedrock | [AWSSDK.BedrockRuntime](https://www.nuget.org/packages/AWSSDK.BedrockRuntime/) | 3.7.301.35 | 4.0.20.1 | — | Instrumented since agent v10.23.0 (InvokeModelAsync); v10.37.0 adds ConverseAsync. |
| OpenAI | [OpenAI](https://www.nuget.org/packages/OpenAI/) | 2.0.0 | 2.8.0 | 10.37.0 | CompleteChat / CompleteChatAsync — only Text completions are supported. |
| Azure OpenAI | [Azure.AI.OpenAI](https://www.nuget.org/packages/Azure.AI.OpenAI/) | 2.0.0 | 2.8.0-beta.1 | 10.37.0 | CompleteChat / CompleteChatAsync — only Text completions are supported. |

### Logging frameworks

| Library | NuGet package | Minimum version | Latest verified | Min agent version | Notes |
| --- | --- | --- | --- | --- | --- |
| Log4Net | [log4net](https://www.nuget.org/packages/log4net/) | 1.2.10 | 3.3.1 | 9.7.0 |  |
| Serilog | [Serilog](https://www.nuget.org/packages/Serilog/) | 1.5.14 | 4.3.1 | 9.7.0 |  |
| NLog | [NLog](https://www.nuget.org/packages/NLog/) | 4.1.2 | 6.1.3 | 9.7.0 |  |
| Microsoft.Extensions.Logging | [Microsoft.Extensions.Logging](https://www.nuget.org/packages/Microsoft.Extensions.Logging/) | 3.0.0 | 10.0.8 | 9.7.0 |  |

### Message systems

The agent automatically instruments these message systems:

| Library | NuGet package | Minimum version | Latest verified | Min agent version | Notes |
| --- | --- | --- | --- | --- | --- |
| Confluent.Kafka | [Confluent.Kafka](https://www.nuget.org/packages/Confluent.Kafka/) | — | — | — | Instruments:<br>`IProducer.Produce`<br>`IProducer.ProduceAsync`<br>`IConsumer.Consume` |
| MSMQ | — | — | — | — | Instruments:<br>`Message.Send`<br>`Message.Receive`<br>`Queue.Peek`<br>`Queue.Purge` |
| NServiceBus | [NServiceBus](https://www.nuget.org/packages/NServiceBus/) | 5.0.0 | 8.2.4 | — |  |
| RabbitMQ | [RabbitMQ.Client](https://www.nuget.org/packages/RabbitMQ.Client/) | 3.6.9 | 6.8.1 | — | BasicGet is instrumented, but distributed tracing is not supported for BasicGet. Only EventingBasicConsumer is instrumented for IBasicConsumer.<br>Versions later than 6.8.1 are supported only when [OpenTelemetry API support](https://docs.newrelic.com/docs/apm/agents/manage-apm-agents/opentelemetry-api-support/) is enabled.<br>Instruments:<br>`IModel.BasicGet`<br>`IModel.BasicPublish`<br>`IModel.BasicConsume`<br>`IModel.QueuePurge`<br>`EventingBasicConsumer.HandleBasicDeliver` |
| MassTransit | [MassTransit](https://www.nuget.org/packages/MassTransit/) | 7.1.0 | 8.5.7 | 10.19.0 |  |
| AWSSDK.SQS | [AWSSDK.SQS](https://www.nuget.org/packages/AWSSDK.SQS/) | 4.0.2.33 | 4.0.2.33 | 10.27.0 |  |
| AWSSDK.Kinesis | [AWSSDK.Kinesis](https://www.nuget.org/packages/AWSSDK.Kinesis/) | 4.0.8.19 | 4.0.8.19 | 10.40.0 | PutRecord(s)Async and GetRecordsAsync are instrumented as message broker operations; other operations as basic method calls. |
| AWSSDK.KinesisFirehose | [AWSSDK.KinesisFirehose](https://www.nuget.org/packages/AWSSDK.KinesisFirehose/) | 4.0.3.32 | 4.0.3.32 | 10.40.0 | All Kinesis Firehose operations are instrumented as basic method calls. |
| Azure Service Bus | [Azure.Messaging.ServiceBus](https://www.nuget.org/packages/Azure.Messaging.ServiceBus/) | 7.11.0 | 7.18.2 | 10.42.0 | Supports Processor mode.<br>Instruments:<br>`Azure.Messaging.ServiceBus.ReceiverManager.ProcessOneMessageWithinScopeAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusProcessor.OnProcessMessageAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusSender.SendMessagesAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusSender.ScheduleMessagesAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusSender.CancelScheduledMessagesAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusReceiver.ReceiveMessagesAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusReceiver.ReceiveDeferredMessagesAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusReceiver.PeekMessagesInternalAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusReceiver.CompleteMessageAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusReceiver.AbandonMessageAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusReceiver.DeadLetterInternalAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusReceiver.DeferMessageAsync`<br>`Azure.Messaging.ServiceBus.ServiceBusReceiver.RenewMessageLockAsync` |

### Background jobs and workflows

| Library | NuGet package | Minimum version | Latest verified | Min agent version | Notes |
| --- | --- | --- | --- | --- | --- |
| Hangfire | [Hangfire](https://www.nuget.org/packages/Hangfire/) | 1.7.15 | 1.8.23 | 10.51.0 |  |
