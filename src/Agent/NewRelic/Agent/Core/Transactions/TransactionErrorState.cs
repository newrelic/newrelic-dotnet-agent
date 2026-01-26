// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Errors;

namespace NewRelic.Agent.Core.Transactions;

public interface ITransactionErrorState : IReadOnlyTransactionErrorState
{
    void AddCustomErrorData(ErrorData errorData);
    void AddExceptionData(ErrorData errorData);
    void AddStatusCodeErrorData(ErrorData errorData);
    void SetIgnoreCustomErrors();
    void SetIgnoreAgentNoticedErrors();
    void TrySetSpanIdForErrorData(ErrorData errorData, string spanId);
}

public interface IReadOnlyTransactionErrorState
{
    bool HasError { get; }
    ErrorData ErrorData { get; }
    string ErrorDataSpanId { get; }
    bool IgnoreCustomErrors { get; }
    bool IgnoreAgentNoticedErrors { get; }
}

public class TransactionErrorState : ITransactionErrorState
{
    private (ErrorData ErrorData, string SpanId) _customErrorData;
    private (ErrorData ErrorData, string SpanId) _transactionExceptionData;
    private (ErrorData ErrorData, string SpanId) _statusCodeErrorData;

    public bool HasError => GetErrorToReport().ErrorData != null;

    public ErrorData ErrorData => GetErrorToReport().ErrorData;
    public string ErrorDataSpanId => GetErrorToReport().SpanId;

    public bool IgnoreCustomErrors { get; private set; }
    public bool IgnoreAgentNoticedErrors { get; private set; }

    private (ErrorData ErrorData, string SpanId) GetErrorToReport()
    {
        if (IgnoreCustomErrors) return (null, null);
        if (_customErrorData.ErrorData != null) return _customErrorData;
        if (IgnoreAgentNoticedErrors) return (null, null);
        return _transactionExceptionData.ErrorData != null ? _transactionExceptionData : _statusCodeErrorData;
    }

    public void AddCustomErrorData(ErrorData errorData)
    {
        if (_customErrorData.ErrorData == null) _customErrorData.ErrorData = errorData;
    }

    public void AddExceptionData(ErrorData errorData)
    {
        if (_transactionExceptionData.ErrorData == null) _transactionExceptionData.ErrorData = errorData;
    }

    public void AddStatusCodeErrorData(ErrorData errorData)
    {
        if (_statusCodeErrorData.ErrorData == null) _statusCodeErrorData.ErrorData = errorData;
    }

    public void SetIgnoreAgentNoticedErrors() => IgnoreAgentNoticedErrors = true;
    public void SetIgnoreCustomErrors() => IgnoreCustomErrors = true;

    public void TrySetSpanIdForErrorData(ErrorData errorData, string spanId)
    {
        if (_customErrorData.ErrorData == errorData) _customErrorData.SpanId = spanId;
        if (_transactionExceptionData.ErrorData == errorData) _transactionExceptionData.SpanId = spanId;
        if (_statusCodeErrorData.ErrorData == errorData) _statusCodeErrorData.SpanId = spanId;
    }
}