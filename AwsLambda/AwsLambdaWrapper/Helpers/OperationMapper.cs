using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace NewRelic.OpenTracing.AmazonLambda.Helpers
{
	internal class OperationMapper
	{
		#region Dictionaries

		private static readonly Dictionary<string, string> _dynamoDBOperations = new Dictionary<string, string>
		{
			{ "CreateTable", "create_table" },
			{ "DeleteItem", "delete_item" },
			{ "DeleteTable", "delete_table" },
			{ "GetItem", "get_item" },
			{ "PutItem", "put_item" },
			{ "Query", "query" },
			{ "Scan", "scan" },
			{ "UpdateItem", "update_item" },
			{ "DescribeTable", "describe_table" }
		};

		private static readonly Dictionary<string, string> _sqsOperations = new Dictionary<string, string>
		{
			{ "ReceiveMessage", "Consume" },
			{ "SendMessage", "Produce" },
			{ "SendMessageBatch", "Produce" }
		};

		private static readonly Dictionary<string, string> _snsOperations = new Dictionary<string, string>
		{
			{ "Publish", "Produce" }
		};

		#endregion

		internal static string GetDynamoDBOperation(HttpRequestMessage httpRequestMessage)
		{
			var header = httpRequestMessage.Headers.GetValues("X-Amz-Target").FirstOrDefault();
			var hsplit = header.Split(new char[] { '.', '_' });

			// If parsing didnt work just return null
			// Here is what the headers should look like
			// "DynamoDB_20120810.DescribeTable"
			// "DynamoDB_20120810.GetItem"
			if (hsplit.Length != 3)
			{
				return null;
			}

			if (!string.IsNullOrEmpty(hsplit[2]) && _dynamoDBOperations.TryGetValue(hsplit[2], out var operation))
			{
				return operation;
			}

			return hsplit[2];
		}

		// Content Looks like this:
		// "Action=SendMessage&DelaySeconds=5&MessageBody=John%20Doe%20customer%20information.&Version=2012-11-05"	
		internal static string GetSQSOperation(string content)
		{
			if (string.IsNullOrEmpty(content))
			{
				return null;
			}

			var contentOperation = content.Split('&')[0].Split('=')[1];
			if (!string.IsNullOrEmpty(contentOperation) && _sqsOperations.TryGetValue(contentOperation, out var operation))
			{
				return operation;
			}

			return contentOperation;
		}

		// Parse Body to retrieve TopicName
		// Looks like this
		// Body: "Action=Publish&Message=Test%20Message&TopicArn=arn%3Aaws%3Asns%3Aus-west-2%3A342444490463%3ADotNetTestSNSTopic&Version=2010-03-31"
		internal static string GetSNSOperation(string content)
		{
			if (string.IsNullOrEmpty(content))
			{
				return null;
			}

			var action = content.Split('&')[0].Split('=');
			if (action.Length != 2)
			{
				return null;
			}

			if (!string.IsNullOrEmpty(action[1]) && _snsOperations.TryGetValue(action[1], out var operation))
			{
				return operation;
			}

			return action[1];
		}
	}
}
