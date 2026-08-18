// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;

namespace ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF;

[Library]
public class WCFServiceSelfHosted
{
    private ServiceHost _wcfService_SelfHosted;

    /// <summary>
    /// Starts the WCF Service with a specific binding and port
    /// </summary>
    /// <param name="bindingType"></param>
    /// <param name="port"></param>
    /// <param name="relativePath"></param>
    [LibraryMethod]
    public void StartService(string bindingType, int port, string relativePath)
    {
        //Debugger.Launch();

        relativePath = relativePath.TrimStart('/');

        if (_wcfService_SelfHosted != null)
        {
            StopService();
        }

        WCFLibraryHelpers.StartAgentWithExternalCall();
        var bindingTypeEnum = (WCFBindingType)Enum.Parse(typeof(WCFBindingType), bindingType, true);
        var baseAddress = WCFLibraryHelpers.GetEndpointAddress(bindingTypeEnum, port, relativePath);
        ConsoleMFLogger.Info($"Starting WCF Service using {bindingTypeEnum} binding at endpoint {baseAddress}");
        _wcfService_SelfHosted = new ServiceHost(typeof(WcfService), baseAddress);
        if (bindingTypeEnum != WCFBindingType.NetTcp)
        {
            var smb = new ServiceMetadataBehavior();
            smb.HttpGetEnabled = true;
            smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
            _wcfService_SelfHosted.Description.Behaviors.Add(smb);
        }

        switch (bindingTypeEnum)
        {
            case WCFBindingType.BasicHttp:
                _wcfService_SelfHosted.AddServiceEndpoint(typeof(IWcfService), new BasicHttpBinding(), baseAddress);
                break;
            case WCFBindingType.WebHttp:
                var endpoint = _wcfService_SelfHosted.AddServiceEndpoint(typeof(IWcfService), new WebHttpBinding(), baseAddress);
                var behavior = new WebHttpBehavior();
                endpoint.EndpointBehaviors.Add(behavior);
                break;
            case WCFBindingType.WSHttp:
                _wcfService_SelfHosted.AddServiceEndpoint(typeof(IWcfService), new WSHttpBinding(), baseAddress);
                break;
            case WCFBindingType.NetTcp:
                _wcfService_SelfHosted.AddServiceEndpoint(typeof(IWcfService), new NetTcpBinding(), baseAddress);
                break;
            case WCFBindingType.Custom:
                _wcfService_SelfHosted.AddServiceEndpoint(typeof(IWcfService), WCFLibraryHelpers.GetCustomBinding(), baseAddress);
                break;
            case WCFBindingType.CustomClass:
                _wcfService_SelfHosted.AddServiceEndpoint(typeof(IWcfService), WCFLibraryHelpers.GetCustomBinding("CustomWcfBinding"), baseAddress);
                break;
            default:
                throw new NotImplementedException($"Binding Type {bindingTypeEnum}");
        }

        _wcfService_SelfHosted.Open();
    }

    /// <summary>
    /// Hosts the minimal INullOperationContextEchoService/NullOperationContextEchoService
    /// contract (see NullOperationContextEcho.cs) with an IOperationInvoker that
    /// dispatches the instrumented SyncMethodInvoker.Invoke onto a fresh thread,
    /// so it runs with no ambient OperationContext.
    ///
    /// Regression coverage for the Wcf3 MethodInvokerWrapper null-dereference in
    /// CaptureHttpRequestHeadersAndMethod.
    ///
    /// This does not delegate to StartService: that method hosts the shared
    /// IWcfService/WcfService contract, whose SyncGetData implementation makes a
    /// real external call to https://www.google.com/ on every invocation. A
    /// tight loop of off-thread calls trips Google's rate limiter after the
    /// first call, which truncates the run with a FaultException unrelated to
    /// the null-OperationContext defect. NetTcp is the only binding this test
    /// uses (see WCFService_Self_NullOperationContext's comment on why), so only
    /// that binding is implemented here.
    /// </summary>
    [LibraryMethod]
    public void StartServiceWithOffThreadInvoker(string bindingType, int port, string relativePath)
    {
        relativePath = relativePath.TrimStart('/');

        if (_wcfService_SelfHosted != null)
        {
            StopService();
        }

        WCFLibraryHelpers.StartAgentWithExternalCall();
        var bindingTypeEnum = (WCFBindingType)Enum.Parse(typeof(WCFBindingType), bindingType, true);
        var baseAddress = WCFLibraryHelpers.GetEndpointAddress(bindingTypeEnum, port, relativePath);
        ConsoleMFLogger.Info($"Starting off-thread-invoker WCF Service using {bindingTypeEnum} binding at endpoint {baseAddress}");
        _wcfService_SelfHosted = new ServiceHost(typeof(NullOperationContextEchoService), baseAddress);
        _wcfService_SelfHosted.Description.Behaviors.Add(new OffThreadInvokerBehavior());

        switch (bindingTypeEnum)
        {
            case WCFBindingType.NetTcp:
                _wcfService_SelfHosted.AddServiceEndpoint(typeof(INullOperationContextEchoService), new NetTcpBinding(), baseAddress);
                break;
            default:
                throw new NotImplementedException($"Binding Type {bindingTypeEnum} is not supported by StartServiceWithOffThreadInvoker");
        }

        _wcfService_SelfHosted.Open();
    }

    /// <summary>
    /// Stops the WCF Service
    /// </summary>
    [LibraryMethod]
    public void StopService()
    {
        _wcfService_SelfHosted?.Close();
    }

}
