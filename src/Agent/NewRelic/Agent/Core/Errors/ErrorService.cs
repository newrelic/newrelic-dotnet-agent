/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Helpers;

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
        ErrorData FromMessage(string errorMessage, IDictionary<string, string> customAttributes);
        ErrorData FromMessage(string errorMessage, IDictionary<string, object> customAttributes);
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
            var exceptionTypeName = GetFriendlyExceptionTypeName(exception);
            if (ShouldIgnoreError(exceptionTypeName)) return true;

            var baseException = exception.GetBaseException();
            var baseExceptionTypeName = GetFriendlyExceptionTypeName(baseException);
            return ShouldIgnoreError(baseExceptionTypeName);
        }

        public bool ShouldIgnoreHttpStatusCode(int statusCode, int? subStatusCode)
        {
            return ShouldIgnoreError(GetFormattedHttpStatusCode(statusCode, subStatusCode));
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

        public ErrorData FromMessage(string errorMessage, IDictionary<string, string> customAttributes)
        {
            var message = _configurationService.Configuration.StripExceptionMessages ? ErrorData.StripExceptionMessagesMessage : errorMessage;
            return new ErrorData(message, CustomErrorTypeName, null, DateTime.UtcNow, CaptureAttributes(customAttributes));
        }

        public ErrorData FromMessage(string errorMessage, IDictionary<string, object> customAttributes)
        {
            var message = _configurationService.Configuration.StripExceptionMessages ? ErrorData.StripExceptionMessagesMessage : errorMessage;
            return new ErrorData(message, CustomErrorTypeName, null, DateTime.UtcNow, CaptureAttributes(customAttributes));
        }

        public ErrorData FromErrorHttpStatusCode(int statusCode, int? subStatusCode, DateTime noticedAt)
        {
            if (statusCode < 400) return null;

            var statusDescription =
#if NETSTANDARD2_0
				statusCode.ToString();
#else
                HttpWorkerRequest.GetStatusDescription(statusCode);
#endif
            var errorTypeName = GetFormattedHttpStatusCode(statusCode, subStatusCode);
            var errorMessage = statusDescription ?? $"Http Error {errorTypeName}";

            return new ErrorData(errorMessage, errorTypeName, null, noticedAt, null);
        }

        private bool ShouldIgnoreError(string errorTypeName)
        {
            if (_configurationService.Configuration.ExceptionsToIgnore.Contains(errorTypeName))
                return true;

            if (_configurationService.Configuration.HttpStatusCodesToIgnore.Contains(errorTypeName))
                return true;

            var splitStatusCode = errorTypeName.Split(StringSeparators.Period);
            if (splitStatusCode[0] != null && _configurationService.Configuration.HttpStatusCodesToIgnore.Contains(splitStatusCode[0]))
            {
                return true;
            }

            return false;
        }

        private string GetFriendlyExceptionTypeName(Exception exception)
        {
            var exceptionTypeName = exception.GetType().FullName;
            return exceptionTypeName?.Split(StringSeparators.BackTick, 2)[0] ?? string.Empty;
        }

        private string GetFormattedHttpStatusCode(int statusCode, int? subStatusCode)
        {
            return subStatusCode == null ? $"{statusCode}" : $"{statusCode}.{subStatusCode}";
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
            return new ErrorData(message, baseExceptionTypeName, stackTrace, noticedAt, customAttributes);
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
