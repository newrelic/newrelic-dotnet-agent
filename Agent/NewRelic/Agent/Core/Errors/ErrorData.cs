using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Helpers;

namespace NewRelic.Agent.Core.Errors
{
	public struct ErrorData
	{
		public readonly string ErrorMessage;

		public readonly string ErrorTypeName;

		public readonly string StackTrace;

		public readonly DateTime NoticedAt;

		public readonly bool IsAnError;

		// used for Custom Error outside a transaction to set value for:
		// 1. ErrorTraceWireModel.Path
		// 2. ErrorEvent transactionName attribute
		public string Path;

		internal static readonly string _stripExceptionMessagesMessage = "Message removed by New Relic based on your currently enabled security settings.";

		private ErrorData(string errorMessage, string errorTypeName, string stackTrace, DateTime noticedAt)
		{
			NoticedAt = noticedAt;
			StackTrace = stackTrace;
			ErrorMessage = errorMessage;
			ErrorTypeName = GetFriendlyExceptionTypeName(errorTypeName);
			IsAnError = true;
			Path = null;
		}

		public static ErrorData FromParts(string errorMessage, string errorTypeName, DateTime noticedAt, bool stripErrorMessage)
		{
			var message = stripErrorMessage ? _stripExceptionMessagesMessage : errorMessage;
			return new ErrorData(message, errorTypeName, null, noticedAt);
		}

		public static ErrorData FromException(Exception exception, bool stripErrorMessage)
		{
			// this does more work than just getting the exception, so we want to do this just once.
			var baseException = exception.GetBaseException();
			var message = stripErrorMessage ? _stripExceptionMessagesMessage : baseException.Message;
			var baseExceptionTypeName = baseException.GetType().FullName;
			// We want the message from the base exception since that is the real exception.
			// We want to show to the stacktace from theouter most excpetion since that will provide the most context for the base exception.
			// See https://newrelic.atlassian.net/browse/DOTNET-4042 for example stacktraces
			var stackTrace = ExceptionFormatter.FormatStackTrace(exception, stripErrorMessage);
			var noticedAt = DateTime.UtcNow;
			return new ErrorData(message, baseExceptionTypeName, stackTrace, noticedAt);
		}

		public static ErrorData TryGetErrorData(ImmutableTransaction immutableTransaction, IEnumerable<string> exceptionsToIgnore, IEnumerable<string> httpStatusCodesToIgnore)
		{
			// *Any* ignored custom error noticed by the agent should result in no error trace
			var customErrors = immutableTransaction.TransactionMetadata.CustomErrorDatas.ToList();
			if (ShouldIgnoreAnyError(customErrors, exceptionsToIgnore, httpStatusCodesToIgnore))
				return new ErrorData();

			// Custom errors are more valuable than exception errors or status code errors
			if (customErrors.Any())
			{
				return customErrors.First();
			}

			// An ignored status code should result in no error trace
			var formattedStatusCode = TryGetFormattedStatusCode(immutableTransaction);
			if (ShouldIgnoreError(formattedStatusCode, exceptionsToIgnore, httpStatusCodesToIgnore))
				return new ErrorData();

			// *Any* ignored exception noticed by the agent should result in no error trace (unless there is a custom error)
			var transactionExceptions = immutableTransaction.TransactionMetadata.TransactionExceptionDatas.ToList();
			if (ShouldIgnoreAnyError(transactionExceptions, exceptionsToIgnore, httpStatusCodesToIgnore))
				return new ErrorData();

			// Exception errors are more valuable than status code errors
			if (transactionExceptions.Any())
			{
				return transactionExceptions.First();
			}

			return TryCreateHttpErrorData(immutableTransaction);
		}

		private static bool ShouldIgnoreAnyError(IEnumerable<ErrorData> errorData, IEnumerable<string> exceptionsToIgnore, IEnumerable<string> httpStatusCodesToIgnore)
		{
			var errorTypeNames = errorData.Select(data => data.ErrorTypeName);
			return ShouldIgnoreAnyError(errorTypeNames, exceptionsToIgnore, httpStatusCodesToIgnore);
		}

		internal static bool ShouldIgnoreError(string errorTypeName, IEnumerable<string> exceptionsToIgnore, IEnumerable<string> httpStatusCodesToIgnore)
		{
			return ShouldIgnoreAnyError(new[] { errorTypeName }, exceptionsToIgnore, httpStatusCodesToIgnore);
		}

		private static bool ShouldIgnoreAnyError(IEnumerable<string> errorTypeNames, IEnumerable<string> exceptionsToIgnore, IEnumerable<string> httpStatusCodesToIgnore)
		{
			foreach (var errorClassName in errorTypeNames)
			{
				if (errorClassName == null)
					continue;

				if (exceptionsToIgnore.Contains(errorClassName))
					return true;

				if (httpStatusCodesToIgnore.Contains(errorClassName))
					return true;

				var splitStatusCode = errorClassName.Split(StringSeparators.Period);
				if (splitStatusCode[0] != null && httpStatusCodesToIgnore.Contains(splitStatusCode[0]))
				{
					return true;
				}
			}

			return false;
		}

		private static string TryGetFormattedStatusCode(ImmutableTransaction immutableTransaction)
		{
			if (immutableTransaction.TransactionMetadata.HttpResponseStatusCode == null)
				return null;

			var statusCode = immutableTransaction.TransactionMetadata.HttpResponseStatusCode.Value;
			var subStatusCode = immutableTransaction.TransactionMetadata.HttpResponseSubStatusCode;

			return subStatusCode == null
				? $"{statusCode}"
				: $"{statusCode}.{subStatusCode}";
		}

		private static ErrorData TryCreateHttpErrorData(ImmutableTransaction immutableTransaction)
		{
			if (immutableTransaction.TransactionMetadata.HttpResponseStatusCode == null)
				return new ErrorData();

			// Only status codes 400+ are considered error status codes
			var statusCode = immutableTransaction.TransactionMetadata.HttpResponseStatusCode.Value;
			if (statusCode < 400)
				return new ErrorData();

			var statusDescription =
#if NETSTANDARD2_0
				statusCode.ToString();
#else
				HttpWorkerRequest.GetStatusDescription(statusCode);
#endif
			var formattedFullStatusCode = TryGetFormattedStatusCode(immutableTransaction);

			var noticedAt = immutableTransaction.StartTime + immutableTransaction.ResponseTimeOrDuration;
			var errorMessage = statusDescription ?? $"Http Error {formattedFullStatusCode}";
			var errorTypeName = formattedFullStatusCode;

			return new ErrorData(errorMessage, errorTypeName, null, noticedAt);
		}

		private static string GetFriendlyExceptionTypeName(string exceptionTypeName)
		{
			return exceptionTypeName?.Split(StringSeparators.BackTick, 2)[0] ?? string.Empty;
		}
	}
}
