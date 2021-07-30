// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Providers.Storage.CallContext;

namespace NewRelic.Providers.Wrapper.HttpWebRequest
{
    public class GetResponseBeginEndWrapper : IWrapper
    {
        private const string ContextSegmentKey = "NewRelic.HttpWebRequest.HttpContextSegmentKey";
        private readonly IContextStorage<ISegment> _contextStorage = new CallContextStorage<ISegment>(ContextSegmentKey);

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo) => new CanWrapResponse(nameof(GetResponseBeginEndWrapper).Equals(methodInfo.RequestedWrapperName));

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            return instrumentedMethodCall.InstrumentedMethodInfo.Method.MethodName == "BeginGetResponse"
                ? BeforeBeginGetResponse(instrumentedMethodCall, agent, transaction)
                : BeforeEndGetResponse(instrumentedMethodCall, agent, transaction);
        }

        private AfterWrappedMethodDelegate BeforeBeginGetResponse(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            transaction.AttachToAsync();

            var httpWebRequest = instrumentedMethodCall.MethodCall.InvocationTarget as System.Net.HttpWebRequest;
            if (httpWebRequest == null)
                throw new NullReferenceException(nameof(httpWebRequest));

            var uri = httpWebRequest.RequestUri;
            if (uri == null)
                return Delegates.NoOp;

            var method = httpWebRequest.Method ?? "<unknown>";
            var transactionExperimental = transaction.GetExperimentalApi();
            var externalSegmentData = transactionExperimental.CreateExternalSegmentData(uri, method);
            var segment = transactionExperimental.StartSegment(instrumentedMethodCall.MethodCall);

            segment.GetExperimentalApi().SetSegmentData(externalSegmentData);
            segment.MakeCombinable();

            _contextStorage.SetData(segment);

            return Delegates.NoOp;
        }

        private AfterWrappedMethodDelegate BeforeEndGetResponse(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            return Delegates.GetDelegateFor<HttpWebResponse>
            (
                onSuccess: response =>
                {
                    var segment = _contextStorage.GetData();
                    var externalSegmentData = segment.GetExperimentalApi().SegmentData as IExternalSegmentData;

                    GetResponseWrapper.TryProcessResponse(response, transaction, segment, externalSegmentData);
                    segment.End();
                },
                onFailure: exception =>
                {
                    var segment = _contextStorage.GetData();
                    var externalSegmentData = segment.GetExperimentalApi().SegmentData as IExternalSegmentData;

                    GetResponseWrapper.TryProcessResponse((exception as WebException)?.Response, transaction, segment, externalSegmentData);
                    segment.End(exception);
                }
            );
        }
    }
}
