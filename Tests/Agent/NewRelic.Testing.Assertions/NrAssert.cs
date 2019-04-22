using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NewRelic.Testing.Assertions
{
	public class NrAssert
	{
		public static T Throws<T>(Action action) where T: Exception
		{
			try
			{
				action();
			}
			catch (T ex)
			{
				return ex;
			}
			catch (Exception ex)
			{
				throw new TestFailureException(
					$"Expected exception of type '{typeof (T)}', but exception of type '{ex.GetType()}' was thrown instead: {Environment.NewLine}{FormatExceptions(new[] {ex})}");
			}

			throw new TestFailureException($"Expected exception of type '{typeof (T)}' was not thrown.");
		}

		public static void Multiple(params Action[] actions)
		{
			var exceptions = new List<Exception>();
			foreach (var action in actions)
			{
				try
				{
					action();
				}
				catch (Exception ex)
				{
					exceptions.Add(ex);
				}
			}

			if (!exceptions.Any())
				return;

			var details = FormatExceptions(exceptions);
			throw new TestFailureException(details);
		}

		private static String FormatExceptions(IEnumerable<Exception> exceptions)
		{
			var strings = new List<String>();

			foreach (var exception in exceptions)
			{
				if (exception == null)
					continue;

				var exceptionName = exception.GetType().Name;
				var failureType = exceptionName.Contains("Assertion") ? "Assertion failed" : exception.GetType().Name;
				var stackTrace = new StackTrace(exception, true);
				var maybeLineNumber = stackTrace.GetFrames()?.FirstOrDefault(frame => frame?.GetFileLineNumber() > 0)?.GetFileLineNumber();
				var lineNumberOrUnknown = maybeLineNumber?.ToString() ?? "[unknown]";

				strings.Add($"{failureType} on line {lineNumberOrUnknown}: {exception.Message}");
			}

			return Environment.NewLine + String.Join(Environment.NewLine, strings.ToArray());
		}
	}

	public class TestFailureException : Exception
	{
		public TestFailureException(String message) : base(message) { }
	}
}
