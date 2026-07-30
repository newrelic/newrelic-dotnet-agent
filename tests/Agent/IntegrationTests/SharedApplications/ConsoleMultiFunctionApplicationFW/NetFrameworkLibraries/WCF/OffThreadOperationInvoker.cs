// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Threading;
using MultiFunctionApplicationHelpers;

namespace ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF;

/// <summary>
/// Wraps the real IOperationInvoker that WCF built for an operation - for a
/// synchronous contract that is
/// System.ServiceModel.Dispatcher.SyncMethodInvoker - and delegates to it from a
/// brand new thread, so the instrumented Invoke runs with no ambient
/// OperationContext and no ambient HttpContext.
///
/// The agent's Wcf3 MethodInvokerWrapper dereferences OperationContext.Current
/// without a null check in CaptureHttpRequestHeadersAndMethod, and that method is
/// reached whenever no transaction is found in context storage. Both ambient
/// contexts are gone on this thread, so both storages miss and the wrapper takes
/// the transaction-creating branch.
///
/// A fresh thread per call is deliberate. On a reused thread, the failing call
/// leaves its transaction orphaned in ThreadLocalStorage and the next call on
/// that thread adopts it - which skips the throwing branch and resets the
/// wrapper's consecutive-failure counter. That makes the failure intermittent.
/// A fresh thread makes it deterministic.
/// </summary>
public class OffThreadOperationInvoker : IOperationInvoker
{
    private readonly IOperationInvoker _inner;

    public OffThreadOperationInvoker(IOperationInvoker inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public bool IsSynchronous => _inner.IsSynchronous;

    public object[] AllocateInputs() => _inner.AllocateInputs();

    public object Invoke(object instance, object[] inputs, out object[] outputs)
    {
        object result = null;
        object[] capturedOutputs = null;
        ExceptionDispatchInfo failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = _inner.Invoke(instance, inputs, out capturedOutputs);
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        })
        {
            IsBackground = true,
            Name = "wcf-off-thread-invoker"
        };

        thread.Start();
        thread.Join();

        outputs = capturedOutputs ?? new object[0];
        failure?.Throw();
        return result;
    }

    public IAsyncResult InvokeBegin(object instance, object[] inputs, AsyncCallback callback, object state)
        => _inner.InvokeBegin(instance, inputs, callback, state);

    public object InvokeEnd(object instance, out object[] outputs, IAsyncResult result)
        => _inner.InvokeEnd(instance, out outputs, result);
}

/// <summary>
/// Installs <see cref="OffThreadOperationInvoker"/> over every dispatch
/// operation.
///
/// Two-stage, and the ordering matters. IServiceBehavior.Validate runs before the
/// DispatchRuntime is built, so it is used to append an IOperationBehavior to
/// each OperationDescription. That operation behavior then runs while the
/// DispatchRuntime is being built, at which point DispatchOperation.Invoker has
/// already been set by the built-in OperationBehaviorAttribute.
///
/// Sweeping ChannelDispatchers from IServiceBehavior.ApplyDispatchBehavior does
/// NOT work: DispatchRuntime.Operations is still empty there, so only
/// UnhandledDispatchOperation would be wrapped and the real SyncMethodInvoker
/// would be left alone.
/// </summary>
public class OffThreadInvokerBehavior : IServiceBehavior
{
    public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
    {
        var attached = 0;

        foreach (var endpoint in serviceDescription.Endpoints)
        {
            if (endpoint.Contract == null || endpoint.Contract.Name == "IMetadataExchange")
            {
                continue;
            }

            foreach (var operation in endpoint.Contract.Operations)
            {
                if (operation.Behaviors.Find<OffThreadInvokerOperationBehavior>() != null)
                {
                    continue;
                }

                operation.Behaviors.Add(new OffThreadInvokerOperationBehavior());
                attached++;
            }
        }

        ConsoleMFLogger.Info($"OffThreadInvokerBehavior attached to {attached} operation(s)");
    }

    public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase,
        Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters)
    {
    }

    public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
    {
    }
}

/// <summary>
/// Per-operation hook that swaps the invoker.
/// </summary>
public class OffThreadInvokerOperationBehavior : IOperationBehavior
{
    public void Validate(OperationDescription operationDescription)
    {
    }

    public void AddBindingParameters(OperationDescription operationDescription, BindingParameterCollection bindingParameters)
    {
    }

    public void ApplyClientBehavior(OperationDescription operationDescription, ClientOperation clientOperation)
    {
    }

    public void ApplyDispatchBehavior(OperationDescription operationDescription, DispatchOperation dispatchOperation)
    {
        if (dispatchOperation.Invoker == null || dispatchOperation.Invoker is OffThreadOperationInvoker)
        {
            return;
        }

        ConsoleMFLogger.Info($"Wrapping invoker for {operationDescription.Name}: {dispatchOperation.Invoker.GetType().FullName}");
        dispatchOperation.Invoker = new OffThreadOperationInvoker(dispatchOperation.Invoker);
    }
}
