// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;

namespace NewRelic.OpenTracing.AmazonLambda.DiagnosticObserver
{
    /// <summary>
    /// CoreFxDiagnosticObserver to subscribe to .net Core Diagnostic Events
    /// </summary>
    internal class CoreFxDiagnosticObserver : IObserver<DiagnosticListener>
    {
        public void OnCompleted()
        {
            // Perhaps this should be a no-op. Throwing an exception for now
            // to see if this gets invoked ever
            throw new NotImplementedException();
        }

        public void OnError(Exception error)
        {
            // Perhaps this should be a no-op. Throwing an exception for now
            // to see if this gets invoked ever
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called for each Listener for a particular Event Name
        /// this will subscribe to CoreFx Events (Http + EF)
        /// </summary>
        /// <param name="value"></param>
        public void OnNext(DiagnosticListener value)
        {
            switch (value.Name)
            {
                // Observe Events from HttpHandler
                case "HttpHandlerDiagnosticListener":
                    value.Subscribe(new HttpClientObserver());
                    break;
            }
        }
    }
}
