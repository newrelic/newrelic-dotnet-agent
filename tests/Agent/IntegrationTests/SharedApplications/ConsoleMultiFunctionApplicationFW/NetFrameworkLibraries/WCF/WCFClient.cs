// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using NewRelic.Api.Agent;
using System;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using System.Threading;

namespace ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF
{
    [Library]
    public class WCFClient
    {
        private IWcfClient _wcfClient;
        private bool _needToPauseForWarmup = true;

        private void InitializeClient(string bindingType, int port, string relativePath)
        {
            relativePath = relativePath.TrimStart('/');
            var bindingTypeEnum = (WCFBindingType)Enum.Parse(typeof(WCFBindingType), bindingType);
            var endpointAddress = WCFLibraryHelpers.GetEndpointAddress(bindingTypeEnum, port, relativePath);

            switch (bindingTypeEnum)
            {
                case WCFBindingType.BasicHttp:
                    _wcfClient = CreateClientWithHttpBinding(endpointAddress);
                    break;
                case WCFBindingType.WSHttp:
                    _wcfClient = CreateClientWithWSHttpBinding(endpointAddress);
                    break;
                case WCFBindingType.WebHttp:
                    _wcfClient = CreateClientWithWebHttpBinding(endpointAddress);
                    break;
                case WCFBindingType.NetTcp:
                    _wcfClient = CreateClientWithNetTCPBinding(endpointAddress);
                    break;
                case WCFBindingType.Custom:
                    _wcfClient = CreateClientWithCustomBinding(endpointAddress);
                    break;
                case WCFBindingType.CustomClass:
                    _wcfClient = CreateClientWithCustomBinding(endpointAddress, "CustomWcfBinding");
                    break;
                default:
                    throw new NotImplementedException($"Binding Type {bindingTypeEnum}");
            }
        }

        /// <summary>
        /// Initializes WCF Client With specific binding on a specific port.
        /// </summary>
        /// <param name="bindingType"></param>
        /// <param name="port"></param>
        [LibraryMethod]
        public void InitializeClient_SelfHosted(string bindingType, int port, string relativePath)
        {
            InitializeClient(bindingType, port, relativePath);
            _needToPauseForWarmup = false;
        }

        /// <summary>
        /// Initializes WCF Client With specific binding on a specific port.
        /// </summary>
        /// <param name="bindingType"></param>
        /// <param name="port"></param>
        [LibraryMethod]
        public void InitializeClient_IISHosted(string bindingType, int port, string relativePath)
        {
            //For IIS, prepend the WcfService.svc to the call
            relativePath = $"WcfService.svc/{relativePath.TrimStart('/')}";
            InitializeClient(bindingType, port, relativePath);
        }

        private void PauseForWarmup()
        {
            Logger.Info($"Pausing on first connection to Hosted Web Core Instance");
            Thread.Sleep(TimeSpan.FromSeconds(5));
            _needToPauseForWarmup = false;
        }

        /// <summary>
        /// Calls the WCF Service GetData function with both client and server invocation methods (sync,begin/end,tap async)
        /// </summary>
        /// <param name="clientInvocationMethod"></param>
        /// <param name="serviceInvocationMethod"></param>
        /// <param name="value"></param>
        [LibraryMethod]
        [Transaction]
        public void GetData(WCFInvocationMethod clientInvocationMethod, WCFInvocationMethod serviceInvocationMethod, int value)
        {
            if (_wcfClient == null)
            {
                throw new InvalidOperationException("WCF Client not instantiated");
            }

            if (_needToPauseForWarmup)
            {
                PauseForWarmup();
            }

            string result = null;
            switch (clientInvocationMethod)
            {
                case WCFInvocationMethod.Sync:
                    switch (serviceInvocationMethod)
                    {
                        case WCFInvocationMethod.Sync:
                            result = _wcfClient.Sync_SyncGetData(value);
                            break;
                        case WCFInvocationMethod.BeginEndAsync:
                            result = _wcfClient.Sync_AsyncGetData(value);
                            break;
                        case WCFInvocationMethod.TAPAsync:
                            result = _wcfClient.Sync_TAPGetData(value);
                            break;
                        default:
                            throw new NotImplementedException($"Client/Service Invocation Method Combo {clientInvocationMethod}/{serviceInvocationMethod}");
                    }
                    break;

                case WCFInvocationMethod.BeginEndAsync:
                    switch (serviceInvocationMethod)
                    {
                        case WCFInvocationMethod.Sync:
                            {
                                var asyncResult = _wcfClient.Begin_SyncGetData(value, null, null);
                                result = _wcfClient.End_SyncGetData(asyncResult);
                            }
                            break;
                        case WCFInvocationMethod.BeginEndAsync:
                            {
                                var asyncResult = _wcfClient.Begin_AsyncGetData(value, null, null);
                                result = _wcfClient.End_AsyncGetData(asyncResult);
                            }
                            break;
                        case WCFInvocationMethod.TAPAsync:
                            {
                                var asyncResult = _wcfClient.Begin_TAPGetData(value, null, null);
                                result = _wcfClient.End_TAPGetData(asyncResult);
                            }
                            break;
                        default:
                            throw new NotImplementedException($"Client/Service Invocation Method Combo {clientInvocationMethod}/{serviceInvocationMethod}");
                    }
                    break;
                case WCFInvocationMethod.TAPAsync:
                    switch (serviceInvocationMethod)
                    {
                        case WCFInvocationMethod.Sync:
                            {
                                result = _wcfClient.TAP_SyncGetData(value).Result;
                            }
                            break;
                        case WCFInvocationMethod.BeginEndAsync:
                            {
                                result = _wcfClient.TAP_AsyncGetData(value).Result;
                            }
                            break;
                        case WCFInvocationMethod.TAPAsync:
                            {
                                result = _wcfClient.TAP_TAPGetData(value).Result;
                            }
                            break;
                        default:
                            throw new NotImplementedException($"Client/Service Invocation Method Combo {clientInvocationMethod}/{serviceInvocationMethod}");
                    }
                    break;
                case WCFInvocationMethod.EventBasedAsync:
                    switch (serviceInvocationMethod)
                    {
                        case WCFInvocationMethod.Sync:
                            {
                                using (var wait = new ManualResetEvent(false))
                                {
                                    _wcfClient.Event_SyncGetData_Completed += (e, a) =>
                                    {
                                        result = a.Result;
                                        wait.Set();
                                    };
                                    _wcfClient.Event_SyncGetData(32);
                                    wait.WaitOne(TimeSpan.FromSeconds(20));
                                }
                            }
                            break;
                        default:
                            throw new NotImplementedException($"Client/Service Invocation Method Combo {clientInvocationMethod}/{serviceInvocationMethod}");
                    }
                    break;
                default:
                    throw new NotImplementedException($"Client Invocation Method {clientInvocationMethod} Not supported");
            }

            Logger.Info($"Result: {result ?? "<NULL>"}");
        }

        /// <summary>
        /// Calls the WCF Service ThrowException function with both client and server invocation methods (sync,begin/end,tap async)
        /// </summary>
        /// <param name="clientInvocationMethod"></param>
        /// <param name="serviceInvocationMethod"></param>
        /// <param name="value"></param>
        [LibraryMethod]
        [Transaction]
        public void ThrowException(WCFInvocationMethod clientInvocationMethod, WCFInvocationMethod serviceInvocationMethod, bool asyncThrowOnBegin)
        {
            if (_wcfClient == null)
            {
                throw new InvalidOperationException("WCF Client not instantiated");
            }

            if (_needToPauseForWarmup)
            {
                PauseForWarmup();
            }

            try
            {
                string result = null;
                switch (clientInvocationMethod)
                {
                    case WCFInvocationMethod.Sync:
                        switch (serviceInvocationMethod)
                        {
                            case WCFInvocationMethod.Sync:
                                result = _wcfClient.Sync_SyncThrowException();
                                break;
                            case WCFInvocationMethod.BeginEndAsync:
                                result = asyncThrowOnBegin
                                    ? _wcfClient.Sync_AsyncThrowExceptionAtStart()
                                    : _wcfClient.Sync_AsyncThrowExceptionAtEnd();
                                break;
                            case WCFInvocationMethod.TAPAsync:
                                result = _wcfClient.Sync_TAPThrowException();
                                break;
                            default:
                                throw new NotImplementedException($"Client/Service Invocation Method Combo {clientInvocationMethod}/{serviceInvocationMethod}");
                        }
                        break;
                    case WCFInvocationMethod.BeginEndAsync:
                        switch (serviceInvocationMethod)
                        {
                            case WCFInvocationMethod.Sync:
                                {
                                    var asyncResult = _wcfClient.Begin_SyncThrowException(null, null);
                                    result = _wcfClient.End_SyncThrowException(asyncResult);
                                }
                                break;
                            case WCFInvocationMethod.BeginEndAsync:
                                {
                                    if (asyncThrowOnBegin)
                                    {
                                        var asyncResult = _wcfClient.Begin_AsyncThrowExceptionAtStart(null, null);
                                        result = _wcfClient.End_AsyncThrowExceptionAtStart(asyncResult);
                                    }
                                    else
                                    {
                                        var asyncResult = _wcfClient.Begin_AsyncThrowExceptionAtEnd(null, null);
                                        result = _wcfClient.End_AsyncThrowExceptionAtEnd(asyncResult);
                                    }
                                }
                                break;
                            case WCFInvocationMethod.TAPAsync:
                                {
                                    var asyncResult = _wcfClient.Begin_TAPThrowException(null, null);
                                    result = _wcfClient.End_TAPThrowException(asyncResult);
                                }
                                break;
                            default:
                                throw new NotImplementedException($"Client/Service Invocation Method Combo {clientInvocationMethod}/{serviceInvocationMethod}");
                        }

                        break;
                    case WCFInvocationMethod.TAPAsync:
                        switch (serviceInvocationMethod)
                        {
                            case WCFInvocationMethod.Sync:
                                {
                                    result = _wcfClient.TAP_SyncThrowException().Result;
                                }
                                break;
                            case WCFInvocationMethod.BeginEndAsync:
                                {
                                    result = asyncThrowOnBegin
                                        ? _wcfClient.TAP_AsyncThrowExceptionAtStart().Result
                                        : _wcfClient.TAP_AsyncThrowExceptionAtEnd().Result;
                                }
                                break;
                            case WCFInvocationMethod.TAPAsync:
                                {
                                    result = _wcfClient.TAP_TAPThrowException().Result;
                                }
                                break;
                            default:
                                throw new NotImplementedException($"Client/Service Invocation Method Combo {clientInvocationMethod}/{serviceInvocationMethod}");
                        }
                        break;
                    case WCFInvocationMethod.EventBasedAsync:
                        switch (serviceInvocationMethod)
                        {
                            case WCFInvocationMethod.Sync:
                                using (var wait = new ManualResetEvent(false))
                                {
                                    _wcfClient.Event_ThrowException_Completed += (e, a) =>
                                    {
                                        if (a.Error != null)
                                        {
                                            result = "Handled Expected WCF Exception";
                                        }
                                        else
                                        {
                                            throw new Exception("Expected WCF Error didn't occur");
                                        }

                                        wait.Set();
                                    };
                                    _wcfClient.Event_SyncThrowException();
                                    wait.WaitOne(TimeSpan.FromSeconds(20));
                                }
                                break;
                            default:
                                throw new NotImplementedException($"Client/Service Invocation Method Combo {clientInvocationMethod}/{serviceInvocationMethod}");
                        }
                        break;
                    default:
                        throw new NotImplementedException($"Client Invocation Method {clientInvocationMethod} Not supported");
                }

                Logger.Info($"Result: {result ?? "<NULL>"}");
            }
            catch (FaultException)

            {
                Logger.Info("Ignoring WCF Fault Exception!");
            }
            catch (AggregateException aggEx)
            {
                if (!(aggEx.InnerException is FaultException) && !(aggEx.InnerException is ProtocolException))
                {
                    throw;
                }

                Logger.Info($"Ignoring AggregateException -> WCF {aggEx.InnerException.GetType()} Exception!");
            }
            catch (TargetInvocationException tgtEx)
            {
                if (!(tgtEx.InnerException is FaultException<ExceptionDetail>))
                {
                    throw;
                }


                Logger.Info("Ignoring TergetInvocationException -> WCF Fault Exception<ExceptionDetail>!");
            }
            catch (ProtocolException)
            {
                Logger.Info("Ignoring ProtocolException");
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        #region Clients

        private WcfClient CreateClientWithWebHttpBinding(Uri endpointAddress)
        {
            var webHttpBehavior = new WebHttpBehavior { DefaultBodyStyle = WebMessageBodyStyle.Bare, DefaultOutgoingRequestFormat = WebMessageFormat.Xml, DefaultOutgoingResponseFormat = WebMessageFormat.Xml };
            var binding = new WebHttpBinding();
            var endpoint = new EndpointAddress(endpointAddress);
            var client = new WcfClient(binding, endpoint);
            client.ChannelFactory.Endpoint.EndpointBehaviors.Add(webHttpBehavior);

            return client;
        }

        private WcfClient CreateClientWithHttpBinding(Uri endpointAddress)
        {
            var binding = new BasicHttpBinding();
            var endpoint = new EndpointAddress(endpointAddress);
            return new WcfClient(binding, endpoint);
        }

        private WcfClient CreateClientWithWSHttpBinding(Uri endpointAddress)
        {
            var binding = new WSHttpBinding();
            var endpoint = new EndpointAddress(endpointAddress);
            return new WcfClient(binding, endpoint);
        }

        private WcfClient CreateClientWithNetTCPBinding(Uri endpointAddress)
        {
            var binding = new NetTcpBinding();
            var endpoint = new EndpointAddress(endpointAddress);

            return new WcfClient(binding, endpoint);
        }

        private WcfClient CreateClientWithCustomBinding(Uri endpointAddress, string configurationName = null)
        {
            var binding = WCFLibraryHelpers.GetCustomBinding(configurationName);
            var endpoint = new EndpointAddress(endpointAddress);
            return new WcfClient(binding, endpoint);
        }

        #endregion
    }
}
