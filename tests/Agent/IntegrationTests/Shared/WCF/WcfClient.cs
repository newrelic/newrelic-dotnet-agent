// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NETFRAMEWORK
using System;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace NewRelic.Agent.IntegrationTests.Shared.Wcf
{
    public interface IWcfClientChannel : IWcfClient, IClientChannel
    {
    }

    public partial class WcfClient : ClientBase<IWcfClient>, IWcfClient
    {
        private const string HandledExpectedErrorMsg = "Handled Expected Service Side Exception";

        #region Constructors

        public WcfClient()
        {
        }

        public WcfClient(string endpointConfigurationName) :
                base(endpointConfigurationName)
        {
        }

        public WcfClient(string endpointConfigurationName, string remoteAddress) :
                base(endpointConfigurationName, remoteAddress)
        {
        }

        public WcfClient(string endpointConfigurationName, EndpointAddress remoteAddress) :
                base(endpointConfigurationName, remoteAddress)
        {
        }

        public WcfClient(System.ServiceModel.Channels.Binding binding, EndpointAddress remoteAddress) :
                base(binding, remoteAddress)
        {
        }

        #endregion


        #region Server = Sync, Client variations

        public string Sync_SyncGetData(int value)
        {
            return Channel.Sync_SyncGetData(value);
        }

        public IAsyncResult Begin_SyncGetData(int value, AsyncCallback callback, object asyncState)
        {
            return Channel.Begin_SyncGetData(value, callback, asyncState);
        }


        public string End_SyncGetData(IAsyncResult result)
        {
            return base.Channel.End_SyncGetData(result);
        }


        public Task<string> TAP_SyncGetData(int value)
        {
            return base.Channel.TAP_SyncGetData(value);
        }



        public string Sync_SyncThrowException()
        {
            return Channel.Sync_SyncThrowException();
        }

        public IAsyncResult Begin_SyncThrowException(AsyncCallback callback, object asyncState)
        {
            return Channel.Begin_SyncThrowException(callback, asyncState);
        }

        public string End_SyncThrowException(IAsyncResult result)
        {
            return Channel.End_SyncThrowException(result);
        }

        public Task<string> TAP_SyncThrowException()
        {
            return Channel.TAP_SyncThrowException();
        }


        #endregion


        #region Server = Begin/End, Client variations

        public string Sync_AsyncGetData(int value)
        {
            return Channel.Sync_AsyncGetData(value);
        }

        public IAsyncResult Begin_AsyncGetData(int value, AsyncCallback callback, object asyncState)
        {
            return this.Channel.Begin_AsyncGetData(value, callback, asyncState);
        }

        public string End_AsyncGetData(IAsyncResult result)
        {
            return this.Channel.End_AsyncGetData(result);
        }

        public Task<string> TAP_AsyncGetData(int value)
        {
            return this.Channel.TAP_AsyncGetData(value);
        }

        public string Sync_AsyncThrowExceptionAtStart()
        {
            return Channel.Sync_AsyncThrowExceptionAtStart();
        }

        public IAsyncResult Begin_AsyncThrowExceptionAtStart(AsyncCallback callback, object asyncState)
        {
            return Channel.Begin_AsyncThrowExceptionAtStart(callback, asyncState);
        }

        public string End_AsyncThrowExceptionAtStart(IAsyncResult result)
        {
            return Channel.End_AsyncThrowExceptionAtStart(result);
        }

        public Task<string> TAP_AsyncThrowExceptionAtStart()
        {
            return Channel.TAP_AsyncThrowExceptionAtStart();
        }


        public string Sync_AsyncThrowExceptionAtEnd()
        {
            return Channel.Sync_AsyncThrowExceptionAtEnd();
        }

        public IAsyncResult Begin_AsyncThrowExceptionAtEnd(AsyncCallback callback, object asyncState)
        {
            return Channel.Begin_AsyncThrowExceptionAtEnd(callback, asyncState);
        }

        public string End_AsyncThrowExceptionAtEnd(IAsyncResult result)
        {
            return Channel.End_AsyncThrowExceptionAtEnd(result);
        }

        public Task<string> TAP_AsyncThrowExceptionAtEnd()
        {
            return Channel.TAP_AsyncThrowExceptionAtEnd();
        }



        #endregion


        #region Server = TAP, Client Variations


        public string Sync_TAPGetData(int value)
        {
            return Channel.Sync_TAPGetData(value);
        }

        public IAsyncResult Begin_TAPGetData(int value, AsyncCallback callback, object asyncState)
        {
            return Channel.Begin_TAPGetData(value, callback, asyncState);
        }

        public string End_TAPGetData(IAsyncResult result)
        {
            return Channel.End_TAPGetData(result);
        }

        public Task<string> TAP_TAPGetData(int value)
        {
            return Channel.TAP_TAPGetData(value);
        }

        public Task<string> TAP_TAPMakeExternalCalls()
        {
            return Channel.TAP_TAPMakeExternalCalls();
        }

        public string Sync_TAPThrowException()
        {
            return Channel.Sync_TAPThrowException();
        }

        public IAsyncResult Begin_TAPThrowException(AsyncCallback callback, object asyncState)
        {
            return Channel.Begin_TAPThrowException(callback, asyncState);
        }

        public string End_TAPThrowException(IAsyncResult result)
        {
            return Channel.End_TAPThrowException(result);
        }

        public Task<string> TAP_TAPThrowException()
        {
            return Channel.TAP_TAPThrowException();
        }

        #endregion


        #region EventHandler Based Async Client

        private BeginOperationDelegate _onBeginGetDataDelegate;
        private EndOperationDelegate _onEndGetDataDelegate;
        private SendOrPostCallback _onGetDataCompletedDelegate;
        private BeginOperationDelegate _onBeginThrowExceptionDelegate;
        private EndOperationDelegate _onEndThrowExceptionDelegate;
        private SendOrPostCallback _onThrowExceptionCompletedDelegate;

        public event System.EventHandler<GetCompletedEventArgs> Event_SyncGetData_Completed;
        public event System.EventHandler<GetCompletedEventArgs> Event_ThrowException_Completed;

        private void OnGetDataCompleted(object state)
        {
            if (this.Event_SyncGetData_Completed != null)
            {
                var e = ((InvokeAsyncCompletedEventArgs)(state));
                this.Event_SyncGetData_Completed(this, new GetCompletedEventArgs(e.Results, e.Error, e.Cancelled, e.UserState));
            }
        }

        public void Event_SyncGetData(int value)
        {
            this.Event_SyncGetData(value, null);
        }

        public void Event_SyncGetData(int value, object userState)
        {
            if (this._onBeginGetDataDelegate == null)
            {
                this._onBeginGetDataDelegate = new BeginOperationDelegate(this.OnBeginGetData);
            }

            if (this._onEndGetDataDelegate == null)
            {
                this._onEndGetDataDelegate = new EndOperationDelegate(this.OnEndGetData);
            }

            if (this._onGetDataCompletedDelegate == null)
            {
                this._onGetDataCompletedDelegate = new SendOrPostCallback(this.OnGetDataCompleted);
            }

            base.InvokeAsync(this._onBeginGetDataDelegate, new object[] { value },
                this._onEndGetDataDelegate, this._onGetDataCompletedDelegate, userState);

        }

        private IAsyncResult OnBeginGetData(object[] inValues, AsyncCallback callback, object asyncState)
        {
            var value = ((int)(inValues[0]));
            return Begin_SyncGetData(value, callback, asyncState);
        }

        private object[] OnEndGetData(IAsyncResult result)
        {
            var retVal = End_SyncGetData(result);
            return new object[] { retVal };
        }

        private void OnThrowExceptionCompleted(object state)
        {
            if (Event_ThrowException_Completed != null)
            {
                var e = ((InvokeAsyncCompletedEventArgs)(state));
                Event_ThrowException_Completed(this, new GetCompletedEventArgs(e.Results, e.Error, e.Cancelled, e.UserState));
            }
        }

        public void Event_SyncThrowException()
        {
            Event_SyncThrowException(null);
        }

        public void Event_SyncThrowException(object userState)
        {
            if (this._onBeginThrowExceptionDelegate == null)
            {
                this._onBeginThrowExceptionDelegate = new BeginOperationDelegate(this.OnBeginThrowException);
            }

            if (this._onEndThrowExceptionDelegate == null)
            {
                this._onEndThrowExceptionDelegate = new EndOperationDelegate(this.OnEndThrowException);
            }

            if (this._onThrowExceptionCompletedDelegate == null)
            {
                this._onThrowExceptionCompletedDelegate = new SendOrPostCallback(this.OnThrowExceptionCompleted);
            }

            base.InvokeAsync(this._onBeginThrowExceptionDelegate, new object[0],
                this._onEndThrowExceptionDelegate, this._onThrowExceptionCompletedDelegate, userState);

        }

        private IAsyncResult OnBeginThrowException(object[] inValues, AsyncCallback callback, object asyncState)
        {
            return this.Begin_SyncThrowException(callback, asyncState);
        }

        private object[] OnEndThrowException(IAsyncResult result)
        {
            var retVal = this.End_SyncThrowException(result);
            return new object[] { retVal };
        }


        #endregion


    }
}

#endif
