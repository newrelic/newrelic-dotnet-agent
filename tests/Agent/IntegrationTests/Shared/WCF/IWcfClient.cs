// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NETFRAMEWORK
using System;
using System.ServiceModel;
using System.Threading.Tasks;

namespace NewRelic.Agent.IntegrationTests.Shared.Wcf
{
    /// <summary>
    /// IWCF Client implements various functions we would like to test.  
    /// Naming convention:  [ClientInvocationMethod]_[ServiceInvocationMethod][Method]
    /// Example:			Sync_TAPGetData indicates Sychronous Client call and TAP-based Server inovocation
    /// 
    /// GetData, ThrowException, IgnoreTransaction were seperate services because there was difficulty
    /// in setting up a WCF service that accepted multiple parameters (like GetData(value, throwexception, ignoreTrx)
    /// </summary>
    [ServiceContract]
    public interface IWcfClient
    {
        #region Server = Sync, Client Variations

        [OperationContract(Name = "SyncGetData", Action = "http://tempuri.org/IWcfService/SyncGetData", ReplyAction = "http://tempuri.org/IWcfService/SyncGetDataResponse")]
        string Sync_SyncGetData(int value);

        [OperationContract(Name = "SyncGetData", AsyncPattern = true, Action = "http://tempuri.org/IWcfService/SyncGetData", ReplyAction = "http://tempuri.org/IWcfService/SyncGetDataResponse")]
        IAsyncResult Begin_SyncGetData(int value, AsyncCallback callback, object asyncState);

        string End_SyncGetData(IAsyncResult result);

        [OperationContract(Name = "SyncGetData", Action = "http://tempuri.org/IWcfService/SyncGetData", ReplyAction = "http://tempuri.org/IWcfService/SyncGetDataResponse")]
        Task<string> TAP_SyncGetData(int value);


        //Sync Client, Sync Server
        [OperationContract(Name = "SyncThrowException", Action = "http://tempuri.org/IWcfService/SyncThrowException", ReplyAction = "http://tempuri.org/IWcfService/SyncThrowExceptionResponse")]
        string Sync_SyncThrowException();

        //Begin/End Client, Sync Server
        [OperationContract(Name = "SyncThrowException", AsyncPattern = true, Action = "http://tempuri.org/IWcfService/SyncThrowException", ReplyAction = "http://tempuri.org/IWcfService/SyncThrowExceptionResponse")]
        IAsyncResult Begin_SyncThrowException(AsyncCallback callback, object asyncState);

        string End_SyncThrowException(IAsyncResult result);

        //TAP Client, Sync Server
        [OperationContract(Name = "SyncThrowException", Action = "http://tempuri.org/IWcfService/SyncThrowException", ReplyAction = "http://tempuri.org/IWcfService/SyncThrowExceptionResponse")]
        Task<string> TAP_SyncThrowException();

        #endregion


        #region Server = TAP, Client Variations

        [OperationContract(Name = "TAPGetData", Action = "http://tempuri.org/IWcfService/TAPGetData", ReplyAction = "http://tempuri.org/IWcfService/TAPGetDataResponse")]
        string Sync_TAPGetData(int value);

        [OperationContract(Name = "TAPGetData", AsyncPattern = true, Action = "http://tempuri.org/IWcfService/TAPGetData", ReplyAction = "http://tempuri.org/IWcfService/TAPGetDataResponse")]
        IAsyncResult Begin_TAPGetData(int value, AsyncCallback callback, object asyncState);

        string End_TAPGetData(IAsyncResult result);

        [OperationContract(Name = "TAPGetData", Action = "http://tempuri.org/IWcfService/TAPGetData", ReplyAction = "http://tempuri.org/IWcfService/TAPGetDataResponse")]
        Task<string> TAP_TAPGetData(int value);

        [OperationContract(Name = "TAPMakeExternalCalls", Action = "http://tempuri.org/IWcfService/TAPMakeExternalCalls", ReplyAction = "http://tempuri.org/IWcfService/TAPMakeExternalCallsResponse")]
        Task<string> TAP_TAPMakeExternalCalls();

        //Sync Client, TAP Server
        [OperationContract(Name = "TAPThrowException", Action = "http://tempuri.org/IWcfService/TAPThrowException", ReplyAction = "http://tempuri.org/IWcfService/TAPThrowExceptionResponse")]
        string Sync_TAPThrowException();

        //Begin/End Client, TAP Server
        [OperationContract(Name = "TAPThrowException", AsyncPattern = true, Action = "http://tempuri.org/IWcfService/TAPThrowException", ReplyAction = "http://tempuri.org/IWcfService/TAPThrowExceptionResponse")]
        IAsyncResult Begin_TAPThrowException(AsyncCallback callback, object asyncState);

        string End_TAPThrowException(IAsyncResult result);


        //TAP Client, TAP Server
        [OperationContract(Name = "TAPThrowException", Action = "http://tempuri.org/IWcfService/TAPThrowException", ReplyAction = "http://tempuri.org/IWcfService/TAPThrowExceptionResponse")]
        Task<string> TAP_TAPThrowException();

        #endregion


        #region Server = Begin/End, Client Variations

        //Client is synchronous, Server is Begin/End, but WCF Internals/Convention removes the Begin from the method name
        [OperationContract(Name = "AsyncGetData", Action = "http://tempuri.org/IWcfService/AsyncGetData", ReplyAction = "http://tempuri.org/IWcfService/AsyncGetDataResponse")]
        string Sync_AsyncGetData(int value);

        //Client is Begin/End and Server is Begin/End
        [OperationContract(Name = "AsyncGetData", AsyncPattern = true, Action = "http://tempuri.org/IWcfService/AsyncGetData", ReplyAction = "http://tempuri.org/IWcfService/AsyncGetDataResponse")]
        IAsyncResult Begin_AsyncGetData(int value, AsyncCallback callback, object asyncState);

        string End_AsyncGetData(IAsyncResult result);

        //Client is TAP, Server is Begin/End
        [OperationContract(Name = "AsyncGetData", Action = "http://tempuri.org/IWcfService/AsyncGetData", ReplyAction = "http://tempuri.org/IWcfService/AsyncGetDataResponse")]
        Task<string> TAP_AsyncGetData(int value);


        //Sync Client, Server is Begin/End
        [OperationContract(Name = "AsyncThrowExceptionAtStart", Action = "http://tempuri.org/IWcfService/AsyncThrowExceptionAtStart", ReplyAction = "http://tempuri.org/IWcfService/AsyncThrowExceptionAtStartResponse")]
        string Sync_AsyncThrowExceptionAtStart();

        //Begin/End Client, Server is Begin/End
        [OperationContract(Name = "AsyncThrowExceptionAtStart", AsyncPattern = true, Action = "http://tempuri.org/IWcfService/AsyncThrowExceptionAtStart", ReplyAction = "http://tempuri.org/IWcfService/AsyncThrowExceptionAtStartResponse")]
        IAsyncResult Begin_AsyncThrowExceptionAtStart(AsyncCallback callback, object asyncState);

        string End_AsyncThrowExceptionAtStart(IAsyncResult result);

        //TAP Client, Server is Begin/End
        [OperationContract(Name = "AsyncThrowExceptionAtStart", Action = "http://tempuri.org/IWcfService/AsyncThrowExceptionAtStart", ReplyAction = "http://tempuri.org/IWcfService/AsyncThrowExceptionAtStartResponse")]
        Task<string> TAP_AsyncThrowExceptionAtStart();



        //Sync Client, Server is Begin/End
        [OperationContract(Name = "AsyncThrowExceptionAtEnd", Action = "http://tempuri.org/IWcfService/AsyncThrowExceptionAtEnd", ReplyAction = "http://tempuri.org/IWcfService/AsyncThrowExceptionAtEndResponse")]
        string Sync_AsyncThrowExceptionAtEnd();

        //Begin/End Client, Server is Begin/End
        [OperationContract(Name = "AsyncThrowExceptionAtEnd", AsyncPattern = true, Action = "http://tempuri.org/IWcfService/AsyncThrowExceptionAtEnd", ReplyAction = "http://tempuri.org/IWcfService/AsyncThrowExceptionAtEndResponse")]
        IAsyncResult Begin_AsyncThrowExceptionAtEnd(AsyncCallback callback, object asyncState);

        string End_AsyncThrowExceptionAtEnd(IAsyncResult result);

        //TAP Client, Server is Begin/End
        [OperationContract(Name = "AsyncThrowExceptionAtEnd", Action = "http://tempuri.org/IWcfService/AsyncThrowExceptionAtEnd", ReplyAction = "http://tempuri.org/IWcfService/AsyncThrowExceptionAtEndResponse")]
        Task<string> TAP_AsyncThrowExceptionAtEnd();


        #endregion


        #region Supporting Event-Based Client, Sync Server

        void Event_SyncThrowException();
        void Event_SyncGetData(int value);

        event EventHandler<GetCompletedEventArgs> Event_SyncGetData_Completed;
        event EventHandler<GetCompletedEventArgs> Event_ThrowException_Completed;

        #endregion;


    }
}

#endif
