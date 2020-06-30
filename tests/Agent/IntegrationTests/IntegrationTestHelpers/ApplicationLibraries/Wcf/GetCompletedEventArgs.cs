/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;

namespace NewRelic.Agent.IntegrationTestHelpers.ApplicationLibraries.Wcf
{
    public partial class GetCompletedEventArgs : System.ComponentModel.AsyncCompletedEventArgs
    {
        private object[] _results;

        public GetCompletedEventArgs(object[] results, Exception exception, bool cancelled, object userState) :
                base(exception, cancelled, userState)
        {
            this._results = results;
        }

        public string Result
        {
            get
            {
                base.RaiseExceptionIfNecessary();
                return (string)this._results[0];
            }
        }
    }
}
