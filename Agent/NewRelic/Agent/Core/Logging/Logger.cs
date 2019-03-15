using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.Logging
{
	public class Logger : ILogger
	{
		public bool IsEnabledFor(Level level)
		{
			switch (level)
			{
				case Level.Finest:
					return Logging.Log.IsFinestEnabled;
				case Level.Debug:
					return Logging.Log.IsDebugEnabled;
				case Level.Info:
					return Logging.Log.IsInfoEnabled;
				case Level.Warn:
					return Logging.Log.IsWarnEnabled;
				case Level.Error:
					return Logging.Log.IsErrorEnabled;
				default:
					return false;
			}
		}

		public void Log(Level level, object message)
		{
			if (!IsEnabledFor(level)) return;
			var messageString = message.ToString();

			switch (level)
			{
				case Level.Finest:
					Logging.Log.Finest(messageString);
					break;
				case Level.Debug:
					Logging.Log.Debug(messageString);
					break;
				case Level.Info:
					Logging.Log.Info(messageString);
					break;
				case Level.Warn:
					Logging.Log.Warn(messageString);
					break;
				case Level.Error:
					Logging.Log.Error(messageString);
					break;
				default:
					break;
			}
		}
	}
}
