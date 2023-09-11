// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NETFRAMEWORK
using System;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Threading.Tasks;

namespace NewRelic.Agent.IntegrationTests.Shared.Wcf
{
    [ServiceContract(ConfigurationName = "IWcfService")]
    public interface IWcfService
    {
        [OperationContract]
        [WebInvoke(BodyStyle = WebMessageBodyStyle.Bare, RequestFormat = WebMessageFormat.Xml, ResponseFormat = WebMessageFormat.Xml)]
        string SyncGetData(int value);


        [OperationContract(AsyncPattern = true)]
        [WebInvoke(BodyStyle = WebMessageBodyStyle.Bare, RequestFormat = WebMessageFormat.Xml, ResponseFormat = WebMessageFormat.Xml)]
        IAsyncResult BeginAsyncGetData(int value, AsyncCallback callback, object asyncState);

        string EndAsyncGetData(IAsyncResult result);


        [OperationContract]
        [WebInvoke(BodyStyle = WebMessageBodyStyle.Bare, RequestFormat = WebMessageFormat.Xml, ResponseFormat = WebMessageFormat.Xml)]
        Task<string> TAPGetData(int value);

        [OperationContract]
        [WebInvoke(BodyStyle = WebMessageBodyStyle.Bare, RequestFormat = WebMessageFormat.Xml, ResponseFormat = WebMessageFormat.Xml)]
        Task<string> TAPMakeExternalCalls();

        [OperationContract]
        [WebInvoke(BodyStyle = WebMessageBodyStyle.Bare, RequestFormat = WebMessageFormat.Xml, ResponseFormat = WebMessageFormat.Xml)]
        string SyncIgnoreTransaction(string input);



        [OperationContract(AsyncPattern = true)]
        [WebInvoke(BodyStyle = WebMessageBodyStyle.Bare, RequestFormat = WebMessageFormat.Xml, ResponseFormat = WebMessageFormat.Xml)]
        IAsyncResult BeginAsyncIgnoreTransaction(string input, AsyncCallback callback, object asyncState);

        string EndAsyncIgnoreTransaction(IAsyncResult result);


        [OperationContract]
        [WebInvoke(BodyStyle = WebMessageBodyStyle.Bare, RequestFormat = WebMessageFormat.Xml, ResponseFormat = WebMessageFormat.Xml)]
        Task<string> TAPIgnoreTransaction(string input);







        [OperationContract]
        [WebInvoke(BodyStyle = WebMessageBodyStyle.Bare, RequestFormat = WebMessageFormat.Xml, ResponseFormat = WebMessageFormat.Xml)]
        string SyncThrowException();


        [OperationContract(AsyncPattern = true)]
        [WebInvoke(BodyStyle = WebMessageBodyStyle.Bare, RequestFormat = WebMessageFormat.Xml, ResponseFormat = WebMessageFormat.Xml)]
        IAsyncResult BeginAsyncThrowExceptionAtStart(AsyncCallback callback, object asyncState);

        string EndAsyncThrowExceptionAtStart(IAsyncResult result);





        [OperationContract(AsyncPattern = true)]
        [WebInvoke(BodyStyle = WebMessageBodyStyle.Bare, RequestFormat = WebMessageFormat.Xml, ResponseFormat = WebMessageFormat.Xml)]
        IAsyncResult BeginAsyncThrowExceptionAtEnd(AsyncCallback callback, object asyncState);

        string EndAsyncThrowExceptionAtEnd(IAsyncResult result);





        [OperationContract]
        [WebInvoke(BodyStyle = WebMessageBodyStyle.Bare, RequestFormat = WebMessageFormat.Xml, ResponseFormat = WebMessageFormat.Xml)]
        Task<string> TAPThrowException();


    }
}

#endif
