﻿using System;

namespace NewRelic.Agent.Core.Errors
{
	public static class ExceptionFormatter
	{
		public static string FormatStackTrace(Exception exception, bool stripErrorMessage)
		{
			var type = exception.GetType().FullName;
			var message = stripErrorMessage ? null : $": {exception.Message}";
			var formattedInnerException = FormatInnerStackTrace(exception.InnerException, stripErrorMessage);
			var formattedStackTrace = exception.StackTrace != null ? System.Environment.NewLine + exception.StackTrace : null;

			var result = $"{type}{message}{formattedInnerException}{formattedStackTrace}";

			return result;
		}

		private static string FormatInnerStackTrace(Exception innerException, bool stripErrorMessage)
		{
			if (innerException == null)
			{
				return string.Empty;
			}

			var stackTrace = " ---> " + FormatStackTrace(innerException, stripErrorMessage) + System.Environment.NewLine +
			                 "   " + "--- End of inner exception stack trace ---";

			return stackTrace;
		}
	}
}
