using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest.Fixtures
{
    /// <summary>
    /// While this object is in scope, log4net will log to a memory appender.
    /// </summary>
    public class Logging : IDisposable
    {
        public readonly log4net.Appender.MemoryAppender MemoryAppender = new log4net.Appender.MemoryAppender();
        public readonly log4net.Repository.Hierarchy.Logger Logger = (log4net.LogManager.GetRepository() as log4net.Repository.Hierarchy.Hierarchy).Root;
        private readonly log4net.Appender.AppenderCollection _previousAppenders = new log4net.Appender.AppenderCollection();

        /// <summary>
        /// Initializes log4net to log to a memory appender which can then be referenced 
        /// </summary>
        public Logging(log4net.Core.Level level = null)
        {
            Logger.Level = level ?? log4net.Core.Level.All;

            Logger.RemoveAllAppenders();
            Logger.AddAppender(MemoryAppender);

            Logger.Repository.Configured = true;
        }

        /// <summary>
        /// When you dispose of this object the memory appender will be removed from the logging system.
        /// </summary>
        public void Dispose()
        {
            Logger.Repository.Configured = false;

            if (Logger.Appenders == null)
                Assert.Fail("We somehow ended up with no log appenders, test is invalid.");

            if (Logger.Appenders.Count != 1)
                Assert.Fail("Someone added or removed log appenders during the execution of this test potentially invalidating it.");

            Logger.RemoveAllAppenders();
        }

        public override String ToString()
        {
            var builder = new StringBuilder();
            var logEvents = MemoryAppender.GetEvents();
            if (logEvents == null)
                return "Nothing was logged.";

            foreach (var logEvent in logEvents)
            {
                if (logEvent == null)
                    continue;

                builder.AppendLine(logEvent.RenderedMessage);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Checks to see if the given message was logged since this object was constructed.
        /// </summary>
        /// <param name="message">The message you want to check for.</param>
        /// <returns>True if the message was logged, false otherwise.</returns>
        public bool HasMessage(string message)
        {
            var events = MemoryAppender.GetEvents();
            foreach (var item in events)
            {
                if (item.MessageObject.ToString() == message)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// checks for messages that begins with a segment
        /// </summary>
        /// <param name="segment"></param>
        /// <returns></returns>
        public bool HasMessageBeginingWith(string segment)
        {
            var events = MemoryAppender.GetEvents();
            return events.Any(item => item.MessageObject.ToString().StartsWith(segment));
        }

        /// <summary>
        /// checks for messages that begins with a segment
        /// </summary>
        /// <param name="segment"></param>
        /// <returns></returns>
        public bool HasMessageThatContains(string segment)
        {
            var events = MemoryAppender.GetEvents();
            return events.Any(item => item.MessageObject.ToString().Contains(segment));
        }

        /// <summary>
        /// Returns the exception associated with the message if it exists.
        /// </summary>
        /// <param name="message">The message to look for in the message collection.</param>
        /// <returns>The exception associated with the given message or null if either the message wasn't found or no exception was associated with the message.</returns>
        public Exception TryGetExceptionForMessage(string message)
        {
            var events = MemoryAppender.GetEvents();
            foreach (var item in events)
            {
                if (item.MessageObject.ToString() == message)
                    return item.ExceptionObject;
            }
            return null;
        }

        /// <summary>
        /// Counts the number of messages that were logged since the construction of this object.
        /// </summary>
        public int MessageCount { get { return MemoryAppender.GetEvents().Length; } }

        /// <summary>
        /// Counts the number of [level] messages that were logged since the construction of this object.
        /// </summary>
        /// <returns>The number of messages logged at [level] level.</returns>
        private int LevelCount(log4net.Core.Level level)
        {
            var events = MemoryAppender.GetEvents();
            int count = 0;
            foreach (var item in events)
            {
                if (item.Level == level)
                {
                    ++count;
                }
            }

            return count;

        }
        public IEnumerable<String> ErrorMessages
        {
            get
            {
                return MemoryAppender.GetEvents()
                    .Where(@event => @event.Level == log4net.Core.Level.Error)
                    .Select(@event => @event.RenderedMessage);
            }
        }

        /// <summary>
        /// Counts the number of error level messages that were logged since construction of this object.
        /// </summary>
        /// <returns></returns>
        public int ErrorCount { get { return LevelCount(log4net.Core.Level.Error); } }

        /// <summary>
        /// Counts the number of warn level messages that were logged since construction of this object.
        /// </summary>
        /// <returns></returns>
        public int WarnCount { get { return LevelCount(log4net.Core.Level.Warn); } }

        /// <summary>
        /// Counts the number of info level messages that were logged since construction of this object.
        /// </summary>
        /// <returns></returns>
        public int InfoCount { get { return LevelCount(log4net.Core.Level.Info); } }

        /// <summary>
        /// Counts the number of debug level messages that were logged since construction of this object.
        /// </summary>
        /// <returns></returns>
        public int DebugCount { get { return LevelCount(log4net.Core.Level.Debug); } }

        /// <summary>
        /// Counts the number of finest level messages that were logged since construction of this object.
        /// </summary>
        /// <returns></returns>
        public int FinestCount { get { return LevelCount(log4net.Core.Level.Finest); } }
    }
}
