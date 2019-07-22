using System;

namespace NewRelic.Core.Logging
{
	public interface ILogger
	{
		bool IsDebugEnabled { get; }
		bool IsErrorEnabled { get; }
		bool IsFinestEnabled { get; }
		bool IsInfoEnabled { get; }
		bool IsWarnEnabled { get; }

		void Debug(Exception exception);
		void Debug(string message);
		void DebugFormat(string format, params object[] args);
		void Error(Exception exception);
		void Error(string message);
		void ErrorFormat(string format, params object[] args);
		void Finest(Exception exception);
		void Finest(string message);
		void FinestFormat(string format, params object[] args);
		void Info(Exception exception);
		void Info(string message);
		void InfoFormat(string format, params object[] args);
		void Warn(Exception exception);
		void Warn(string message);
		void WarnFormat(string format, params object[] args);
	}
}