// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Helpers;
using System.Collections.ObjectModel;

namespace NewRelic.Agent.Core.Errors
{
    public interface IErrorService
    {
        bool ShouldCollectErrors { get; }

        bool ShouldIgnoreException(Exception exception);
        bool ShouldIgnoreHttpStatusCode(int statusCode, int? subStatusCode);

        ErrorData FromException(Exception exception);
        ErrorData FromException(Exception exception, IDictionary<string, string> customAttributes);
        ErrorData FromException(Exception exception, IDictionary<string, object> customAttributes);
        ErrorData FromMessage(string errorMessage, IDictionary<string, string> customAttributes, bool isExpected);
        ErrorData FromMessage(string errorMessage, IDictionary<string, object> customAttributes, bool isExpected);
        ErrorData FromErrorHttpStatusCode(int statusCode, int? subStatusCode, DateTime noticedAt);
    }

    public class ErrorService : IErrorService
    {
        private const string CustomErrorTypeName = "Custom Error";
        private static ReadOnlyDictionary<string, object> _emptyCustomAttributes = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
        private IConfigurationService _configurationService;

        public ErrorService(IConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }

        public bool ShouldCollectErrors => _configurationService.Configuration.ErrorCollectorEnabled;

        public bool ShouldIgnoreException(Exception exception)
        {
            return IsErrorFromExceptionSpecified(exception, _configurationService.Configuration.IgnoreErrorsConfiguration);
        }

        public bool ShouldIgnoreHttpStatusCode(int statusCode, int? subStatusCode)
        {
            var formattedStatusCode = GetFormattedHttpStatusCode(statusCode, subStatusCode);

            if (_configurationService.Configuration.IgnoreErrorsConfiguration.ContainsKey(formattedStatusCode))
            {
                return true;
            }

            var splitStatusCode = formattedStatusCode.Split(StringSeparators.Period);

            if (splitStatusCode[0] != null && _configurationService.Configuration.IgnoreErrorsConfiguration.ContainsKey(splitStatusCode[0]))
            {
                return true;
            }

            return false;
        }

        public ErrorData FromException(Exception exception)
        {
            return FromExceptionInternal(exception, _emptyCustomAttributes);
        }

        public ErrorData FromException(Exception exception, IDictionary<string, string> customAttributes)
        {
            return FromExceptionInternal(exception, CaptureAttributes(customAttributes));
        }

        public ErrorData FromException(Exception exception, IDictionary<string, object> customAttributes)
        {
            return FromExceptionInternal(exception, CaptureAttributes(customAttributes));
        }

        public ErrorData FromMessage(string errorMessage, IDictionary<string, string> customAttributes, bool isExpected)
        {
            return FromMessageInternal(errorMessage, customAttributes, isExpected);
        }

        public ErrorData FromMessage(string errorMessage, IDictionary<string, object> customAttributes, bool isExpected)
        {
            return FromMessageInternal(errorMessage, customAttributes, isExpected);
        }

        public ErrorData FromErrorHttpStatusCode(int statusCode, int? subStatusCode, DateTime noticedAt)
        {
            if (statusCode < 400) return null;

            var statusDescription =
#if NET
				statusCode.ToString();
#else
                HttpWorkerRequest.GetStatusDescription(statusCode);
#endif
            var errorTypeName = GetFormattedHttpStatusCode(statusCode, subStatusCode);
            var errorMessage = statusDescription ?? $"Http Error {errorTypeName}";

            var isExpected = _configurationService.Configuration.ExpectedStatusCodes.Any(rule => rule.IsMatch(statusCode.ToString()));

            return new ErrorData(errorMessage, errorTypeName, null, noticedAt, null, isExpected, null);
        }

        private static string GetFriendlyExceptionTypeName(Exception exception)
        {
            var exceptionTypeName = exception.GetType().FullName;
            return exceptionTypeName?.Split(StringSeparators.BackTick, 2)[0] ?? string.Empty;
        }

        private string GetFormattedHttpStatusCode(int statusCode, int? subStatusCode)
        {
            return subStatusCode == null ? $"{statusCode}" : $"{statusCode}.{subStatusCode}";
        }

        private ErrorData FromMessageInternal<T>(string errorMessage, IDictionary<string, T> customAttributes, bool isExpected)
        {
            var message = _configurationService.Configuration.StripExceptionMessages ? ErrorData.StripExceptionMessagesMessage : errorMessage;
            return new ErrorData(message, CustomErrorTypeName, null, DateTime.UtcNow, CaptureAttributes(customAttributes), isExpected, null);
        }

        private ErrorData FromExceptionInternal(Exception exception, ReadOnlyDictionary<string, object> customAttributes)
        {
            // this does more work than just getting the exception, so we want to do this just once.
            var baseException = exception.GetBaseException();
            var message = _configurationService.Configuration.StripExceptionMessages ? ErrorData.StripExceptionMessagesMessage : baseException.Message;
            var baseExceptionTypeName = GetFriendlyExceptionTypeName(baseException);
            // We want the message from the base exception since that is the real exception.
            // We want to show to the stacktace from the outermost exception since that will provide the most context for the base exception.
            var stackTrace = ExceptionFormatter.FormatStackTrace(exception, _configurationService.Configuration.StripExceptionMessages);
            var noticedAt = DateTime.UtcNow;

            var isExpected = IsErrorFromExceptionSpecified(exception, _configurationService.Configuration.ExpectedErrorsConfiguration);
            return new ErrorData(message, baseExceptionTypeName, stackTrace, noticedAt, customAttributes, isExpected, baseException);
        }

        private static bool IsErrorFromExceptionSpecified(Exception exception, IDictionary<string, IEnumerable<string>> source)
        {
            var isSpecified = IsExceptionSpecified(exception, source);

            if (!isSpecified)
            {
                var baseException = exception.GetBaseException();
                return IsExceptionSpecified(baseException, source);
            }
            return isSpecified;
        }

        private static bool IsExceptionSpecified(Exception exception, IDictionary<string, IEnumerable<string>> source)
        {
            var exceptionTypeName = GetFriendlyExceptionTypeName(exception);

            if (source.TryGetValue(exceptionTypeName, out var messages))
            {
                if (messages != Enumerable.Empty<string>())
                {
                    return ContainsSubstring(messages, exception.Message);
                }
                else
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsSubstring(IEnumerable<string> subStringList, string sourceString)
        {
            foreach (var item in subStringList)
            {
                if (sourceString.Contains(item))
                {
                    return true;
                }
            }

            return false;
        }

        private ReadOnlyDictionary<string, object> CaptureAttributes<T>(IDictionary<string, T> attributes)
        {
            IDictionary<string, object> result = null;
            if (attributes != null && _configurationService.Configuration.CaptureCustomParameters)
            {
                foreach (var customAttribute in attributes)
                {
                    if (customAttribute.Key != null && customAttribute.Value != null)
                    {
                        if (result == null) result = new Dictionary<string, object>();
                        result.Add(customAttribute.Key, customAttribute.Value);
                    }
                }
            }

            return result == null ? _emptyCustomAttributes : new ReadOnlyDictionary<string, object>(result);
        }
    }
}
