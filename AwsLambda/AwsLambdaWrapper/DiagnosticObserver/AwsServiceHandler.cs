using NewRelic.OpenTracing.AmazonLambda.Helpers;
using OpenTracing;
using System;
using System.Net.Http;

namespace NewRelic.OpenTracing.AmazonLambda.DiagnosticObserver
{
	/// <summary>
	/// AWS Service Handler to parse outbound HttpClient requests
	/// to determine service specifics for DynamoDB, SQS, SNS
	/// </summary>
	internal class AwsServiceHandler
	{
		private const string UseDTWrapperEnvVar = "NEW_RELIC_USE_DT_WRAPPER";

		private static bool? _useDTWrapper = null;
		internal static bool UseDTWrapper => _useDTWrapper ?? UseDTWrapperValueFactory();

		internal static Func<bool> UseDTWrapperValueFactory = DefaultUseDTWrapperValueFactoryImpl;

		/// <summary>
		/// Create AWS Service spans based on HttpRequestMessage
		/// if for some reason we are not able to parse the requests 
		/// return null so an external request will be captured
		/// </summary>
		/// <param name="request"></param>
		/// <returns></returns>
		internal static ISpan CreateAWSSpans(HttpRequestMessage request)
		{
			ISpan span = null;
			var host = request.Headers.Host;
			var split = host.Split(new char[] { '.' });
			switch (split[0])
			{
				case "dynamodb":
					span = ServiceHelpers.CreateSpan(request.GetDynamoDBOperationData(), "DynamoDB");
					break;
				case "sns":
					if (!UseDTWrapper)
					{
						span = ServiceHelpers.CreateSpan(request.GetSNSOperationData(), "SNS");
					}

					break;
				case "sqs":
					if (!UseDTWrapper)
					{
						span = ServiceHelpers.CreateSpan(request.GetSQSOperationData(), "SQS");
					}

					break;
				default:
					break;
			}

			return span;
		}

		private static bool DefaultUseDTWrapperValueFactoryImpl()
		{
			var envVarValue = Environment.GetEnvironmentVariable(UseDTWrapperEnvVar);
			if (bool.TryParse(envVarValue, out bool parseResult))
			{
				_useDTWrapper = parseResult;
			}
			else
			{
				_useDTWrapper = false;
			}

			if (_useDTWrapper.Value)
			{
				Logger.Log(message: $"{UseDTWrapperEnvVar} is set to true; SQS and SNS calls will not be instrumented unless they are wrapped with an SQSWrapper or SNSWrapper method.", rawLogging: false, level: "DEBUG");
			}
			else
			{
				Logger.Log(message: $"{UseDTWrapperEnvVar} is not set to true; SQS and SNS calls will be automatically instrumented, but distributed tracing will not be supported over SQS or SNS.", rawLogging: false, level: "DEBUG");
			}

			return _useDTWrapper.Value;
		}


	}
}
