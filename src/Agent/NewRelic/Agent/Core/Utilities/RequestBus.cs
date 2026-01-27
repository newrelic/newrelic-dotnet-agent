// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading;

namespace NewRelic.Agent.Core.Utilities;

/// <summary>
/// A global request bus for publishing requests that need a response.
/// </summary>
/// <remarks>When you post a request you will get either an enumeration containing a response from every responder or just the first response, depending on which Post overload you use.
/// 
/// Responders are not required to answer and there may not be a responder setup for any given request so you must be prepared to handle either no callback, an empty enumeration or default(TResponse), depending on which Post overload you use.</remarks>
public static class RequestBus<TRequest, TResponse>
{
    public delegate void ResponsesCallback(IEnumerable<TResponse> responses);

    public delegate void ResponseCallback(TResponse response);

    public delegate void RequestHandler(TRequest request, ResponseCallback callback);

    private static readonly IList<RequestHandler> RequestHandlers = new List<RequestHandler>();

    private static readonly ReaderWriterLock Lock = new ReaderWriterLock();
    private static readonly ReaderLockGuard ReaderLockGuard = new ReaderLockGuard(Lock);
    private static readonly WriterLockGuard WriterLockGuard = new WriterLockGuard(Lock);

    public static void AddResponder(RequestHandler requestHandler)
    {
        ValidateTResponse();

        using (WriterLockGuard.Acquire())
        {
            RequestHandlers.Remove(requestHandler);
            RequestHandlers.Add(requestHandler);
        }
    }

    public static void RemoveResponder(RequestHandler requestHandler)
    {
        ValidateTResponse();

        using (WriterLockGuard.Acquire())
        {
            RequestHandlers.Remove(requestHandler);
        }
    }

    /// <summary>
    /// Post a request to this bus and receive an enumeration of responses from all available responders.  Enumeration may be empty.
    /// </summary>
    public static void Post(TRequest request, ResponsesCallback responsesCallback)
    {
        ValidateTResponse();

        List<RequestHandler> oldHandlers;
        using (ReaderLockGuard.Acquire())
        {
            oldHandlers = new List<RequestHandler>(RequestHandlers);
        }

        var responses = new List<TResponse>();
        foreach (var handler in oldHandlers)
        {
            if (handler == null) continue;
            try
            {
                handler(request, responses.Add);
            }
            catch (Exception exception)
            {
                Serilog.Log.Logger.Error(exception, "Exception thrown from request handler.  Request handlers should not let exceptions bubble out of them.");
            }
        }

        responsesCallback(responses);
    }

    /// <summary>
    /// Post a request to this bus and receive a callback from the first responder.  Callback is not guaranteed to be called.
    /// </summary>
    public static void Post(TRequest request, ResponseCallback responseCallback)
    {
        ValidateTResponse();

        IEnumerable<TResponse> responses = null;
        Post(request, innerResponses => responses = innerResponses);
        if (responses == null) return;
        foreach (var response in responses)
        {
            responseCallback(response);
            return;
        }
    }

    /// <summary>
    /// Post a request to this bus and receive the result as a return value.  If there are no hanglers that call back then default(TResponse) will be returned.
    /// </summary>
    /// <example><![CDATA[
    /// var myRequest = new Object();
    /// if (RequestBus<Object, Boolean?>.Post(myRequest) == null) Console.WriteLine("No one is listening.");
    /// if (RequestBus<Object, Boolean?>.Post(myRequest) ?? false) Console.WriteLine("Someone is listening and returned true.");
    /// if (!(RequestBus<Object, Boolean?>.Post(myRequest) ?? true)) Console.WriteLine("Someone is listening and returned false.");
    /// if (RequestBus<Object, Boolean?>.Post(myRequest) ?? true) Console.WriteLine("Either no one is listening or someone returned true.");
    /// if (!(RequestBus<Object, Boolean?>.Post(myRequest) ?? false)) Console.WriteLine("Either no one is listening or someone returned false.");
    /// ]]>
    /// </example>
    public static TResponse Post(TRequest request)
    {
        ValidateTResponse();

        var result = default(TResponse);
        Post(request, receivedResponse => result = receivedResponse);
        return result;
    }

    private static void ValidateTResponse()
    {
        var typeOfTResponse = typeof(TResponse);

        if (!typeOfTResponse.IsValueType) return;
        if (typeOfTResponse.IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Nullable<>)) return;

        throw new ArgumentException("RequestBus only works on nullable types.");
    }
}