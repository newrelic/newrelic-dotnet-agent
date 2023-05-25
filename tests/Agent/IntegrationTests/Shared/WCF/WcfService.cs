// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System;
using System.Net;
using System.Net.Http;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace NewRelic.Agent.IntegrationTests.Shared.Wcf
{
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class WcfService : IWcfService
    {

        private readonly bool _printOutput = false;
        private const string TEST_ERROR_MESAGE = "WCF Service Testing Exception";

#region Supporting GetData

        public string SyncGetData(int value)
        {
            if (_printOutput) Console.WriteLine("SyncGetData");

            return DoWork(value.ToString(), false, false).Result;
        }

        public IAsyncResult BeginAsyncGetData(int value, AsyncCallback callback, object asyncState)
        {
            if (_printOutput) Console.WriteLine("BeginAsyncGetData");

            var tcs = new TaskCompletionSource<string>(asyncState);

            var task = DoWork(value.ToString(), false, false);
            task.ContinueWith(x =>
            {
                tcs.SetResult(x.Result);
                callback(tcs.Task);

            });
            return tcs.Task;
        }

        public string EndAsyncGetData(IAsyncResult result)
        {
            if (_printOutput) Console.WriteLine("EndAsyncGetData");

            var asyncResult = result as Task<string>;

            if (asyncResult == null)
            {
                throw new Exception("Could not cast asyncResult to Task<string>");
            }

            return asyncResult.Result;
        }

        public async Task<string> TAPGetData(int value)
        {
            if (_printOutput) Console.WriteLine("TAPGetData");
            return await DoWork(value.ToString(), false, false);
        }

        public async Task<string> TAPMakeExternalCalls()
        {
            using (var httpClient = new HttpClient())
            {
                _ = await httpClient.GetAsync("https://google.com");
                _ = await httpClient.GetAsync("https://bing.com");
                _ = await httpClient.GetAsync("https://yahoo.com");
            }

            return "OK";
        }

#endregion


#region Supporting IgnoreTransaction

        public string SyncIgnoreTransaction(string input)
        {
            if (_printOutput) Console.WriteLine("SyncIgnoreTransaction");

            return DoWork(input, true, false).Result;
        }

        public IAsyncResult BeginAsyncIgnoreTransaction(string input, AsyncCallback callback, object asyncState)
        {
            if (_printOutput) Console.WriteLine("BeginAsyncIgnoreTransaction");

            var tcs = new TaskCompletionSource<string>(asyncState);

            var task = DoWork(input, true, false);
            task.ContinueWith(x =>
            {
                tcs.SetResult(x.Result);
                callback(tcs.Task);

            });
            return tcs.Task;
        }

        public string EndAsyncIgnoreTransaction(IAsyncResult result)
        {
            if (_printOutput) Console.WriteLine("EndAsyncIgnoreTransaction");

            var asyncResult = result as Task<string>;

            if (asyncResult == null)
            {
                throw new Exception("Could not cast asyncResult to Task<string>");
            }

            return asyncResult.Result;
        }

        public async Task<string> TAPIgnoreTransaction(string input)
        {
            if (_printOutput) Console.WriteLine("TAPIgnoreTransaction");

            return await DoWork(input, true, false);
        }

#endregion


#region Supporting ThrowException


        public string SyncThrowException()
        {
            if (_printOutput) Console.WriteLine("SyncThrowException");
            return DoWork(string.Empty, false, true).Result;
        }

        public async Task<string> TAPThrowException()
        {
            if (_printOutput) Console.WriteLine("TAPThrowException");
            return await DoWork(string.Empty, false, true);
        }

        public IAsyncResult BeginAsyncThrowExceptionAtStart(AsyncCallback callback, object asyncState)
        {
            if (_printOutput) Console.WriteLine("BeginAsyncThrowException");

            NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.AddCustomAttribute("custom key", "custom value");
            NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.AddCustomAttribute("custom foo", "custom bar");

            throw new Exception(TEST_ERROR_MESAGE);
        }

        public string EndAsyncThrowExceptionAtStart(IAsyncResult result)
        {
            if (_printOutput) Console.WriteLine("EndAsyncThrowException");

            var asyncResult = result as Task<string>;

            if (asyncResult == null)
            {
                throw new Exception("Could not cast asyncResult to Task<string>");
            }

            return asyncResult.Result;
        }



        public IAsyncResult BeginAsyncThrowExceptionAtEnd(AsyncCallback callback, object asyncState)
        {
            if (_printOutput) Console.WriteLine("BeginAsyncThrowException");

            var tcs = new TaskCompletionSource<string>(asyncState);

            var task = DoWork(string.Empty, false, false);

            task.ContinueWith(x =>
            {
                if (x.IsFaulted)
                {
                    tcs.SetException(x.Exception);
                }
                else
                {

                    tcs.SetResult(x.Result);
                }

                callback(tcs.Task);
            });
            return tcs.Task;
        }

        public string EndAsyncThrowExceptionAtEnd(IAsyncResult result)
        {
            if (_printOutput) Console.WriteLine("EndAsyncThrowException");

            var asyncResult = result as Task<string>;

            if (asyncResult == null)
            {
                throw new Exception("Could not cast asyncResult to Task<string>");
            }


            throw new Exception(TEST_ERROR_MESAGE);
        }


#endregion


        private async Task<string> DoWork(string value, bool ignoreTransaction, bool throwException)
        {
            if (_printOutput) Console.WriteLine("DoWork");

            NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.AddCustomAttribute("custom key", "custom value");
            NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.AddCustomAttribute("custom foo", "custom bar");

            if (ignoreTransaction)
            {
                NewRelic.Api.Agent.NewRelic.IgnoreTransaction();
            }

            if (throwException)
            {
                throw new Exception(TEST_ERROR_MESAGE);
            }

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            using (var client = new HttpClient())
            {
                var s = await client.GetStringAsync(new Uri("https://www.google.com/"));
                if (_printOutput)
                {
                    Console.WriteLine($"Length of downloaded string = {s.Length}");
                }

                return $"You entered: {value}";
            }
        }
    }
}
#endif
