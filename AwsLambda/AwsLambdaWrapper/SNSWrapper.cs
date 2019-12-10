using Amazon.SimpleNotificationService.Model;
using NewRelic.OpenTracing.AmazonLambda.DiagnosticObserver;
using NewRelic.OpenTracing.AmazonLambda.Helpers;
using OpenTracing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NewRelic.OpenTracing.AmazonLambda
{
	public class SNSWrapper
	{
		#region Public wrapper methods

		/// <summary>
		/// Wrap an SNS Publish request given a PublishRequest object
		/// This will create a client span with component "SNS" and adds the appropriate distributed tracing attribute to the message
		/// Note that this will only have an effect if the environment variable "NEW_RELIC_USE_DT_WRAPPER" is set to "true"
		/// </summary>
		/// <returns>
		/// A Task with type PublishResponse
		/// </returns>
		/// <param name="handler">A function which takes a PublishRequest object and returns a Task of type PublishResponse, e.g. AmazonSNSClient.Publish</param>
		/// <param name "publishRequest">An Amazon.SimpleNotificationService.Model.PublishRequest object</param>
		public static Task<PublishResponse> WrapRequest(Func<PublishRequest, Task<PublishResponse>> handler, PublishRequest publishRequest)
		{
			return WrapPublishRequest(handler, publishRequest);
		}

		/// <summary>
		/// Wrap an SNS Publish request given a PublishRequest object
		/// This will create a client span with component "SNS" and adds the appropriate distributed tracing attribute to the message
		/// Note that this will only have an effect if the environment variable "NEW_RELIC_USE_DT_WRAPPER" is set to "true"
		/// </summary>
		/// <returns>
		/// A Task with type PublishResponse
		/// </returns>
		/// <param name="handler">A function which takes a PublishRequest object and returns a Task of type PublishResponse, e.g. AmazonSNSClient.Publish</param>
		/// <param name "publishRequest">An Amazon.SimpleNotificationService.Model.PublishRequest object</param>
		/// <param name="cancellationToken">An optional CancellationToken object</param>
		public static Task<PublishResponse> WrapRequest(Func<PublishRequest, CancellationToken, Task<PublishResponse>> handler, PublishRequest publishRequest, CancellationToken cancellationToken = default)
		{
			return WrapPublishRequest(handler, publishRequest, cancellationToken);
		}

		/// <summary>
		/// Wrap an SNS Publish request given a topic and message, from which a new PublishRequest object will be created
		/// This will create a client span with component "SNS" and adds the appropriate distributed tracing attribute to the message
		/// Note that this will only have an effect if the environment variable "NEW_RELIC_USE_DT_WRAPPER" is set to "true"
		/// </summary>
		/// <returns>
		/// A Task with type PublishResponse
		/// </returns>
		/// <param name="handler">A function which takes a PublishRequest object and returns a Task of type PublishResponse, e.g. AmazonSNSClient.Publish</param>
		/// <param name="topicArn">An string containing an SNS topic ARN</param>
		/// <param name="message">The message to be published</param>
		/// <param name="cancellationToken">An optional CancellationToken object</param>
		public static Task<PublishResponse> WrapRequest(Func<PublishRequest, CancellationToken, Task<PublishResponse>> handler, string topicArn, string message, CancellationToken cancellationToken = default)
		{
			var publishRequest = new PublishRequest(topicArn, message);
			return WrapPublishRequest(handler, publishRequest, cancellationToken);
		}

		/// <summary>
		/// Wrap an SNS Publish request given a topic, message and subject, from which a new PublishRequest object will be created
		/// This will create a client span with component "SNS" and adds the appropriate distributed tracing attribute to the message
		/// Note that this will only have an effect if the environment variable "NEW_RELIC_USE_DT_WRAPPER" is set to "true"
		/// </summary>
		/// <returns>
		/// A Task with type PublishResponse
		/// </returns>
		/// <param name="handler">A function which takes a PublishRequest object and returns a Task of type PublishResponse, e.g. AmazonSNSClient.Publish</param>
		/// <param name="topicArn">An string containing an SNS topic ARN</param>
		/// <param name="message">The message to be published</param>
		/// <param name="subject">The message subject</param>
		/// <param name="cancellationToken">An optional CancellationToken object</param>
		public static Task<PublishResponse> WrapRequest(Func<PublishRequest, CancellationToken, Task<PublishResponse>> handler, string topicArn, string message, string subject, CancellationToken cancellationToken = default)
		{
			var publishRequest = new PublishRequest(topicArn, message, subject);
			return WrapPublishRequest(handler, publishRequest, cancellationToken);
		}

		#endregion

		#region Private wrapper helpers

		private static Task<PublishResponse> WrapPublishRequest(Func<PublishRequest, CancellationToken, Task<PublishResponse>> handler, PublishRequest publishRequest, CancellationToken cancellationToken = default)
		{
			var span = BeforeWrappedMethod(publishRequest);
			var result = handler(publishRequest, cancellationToken);
			result.ContinueWith((task) => ServiceHelpers.AfterWrappedMethod(span, task), TaskContinuationOptions.ExecuteSynchronously);
			return result;
		}

		private static Task<PublishResponse> WrapPublishRequest(Func<PublishRequest, Task<PublishResponse>> handler, PublishRequest publishRequest)
		{
			var span = BeforeWrappedMethod(publishRequest);
			var result = handler(publishRequest);
			result.ContinueWith((task) => ServiceHelpers.AfterWrappedMethod(span, task), TaskContinuationOptions.ExecuteSynchronously);
			return result;
		}

		#endregion

		#region BeforeWrappedMethod

		private static ISpan BeforeWrappedMethod(PublishRequest publishRequest)
		{
			if (AwsServiceHandler.UseDTWrapper)
			{
				var span = ServiceHelpers.CreateSpan(publishRequest.GetOperationName(ServiceHelpers.ProduceOperation), "SNS", ServiceHelpers.ProduceOperation);
				span.ApplyDistributedTracePayload(publishRequest.MessageAttributes);
				return span;
			}
			else
			{
				return null;
			}
		}

		#endregion
	}
}
