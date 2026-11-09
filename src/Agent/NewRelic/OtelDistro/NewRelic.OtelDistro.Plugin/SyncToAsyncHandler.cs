// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http;
#if NET
using System.Threading;
#endif

namespace NewRelic.OtelDistro.Plugin;

/// <summary>
/// Outermost <see cref="DelegatingHandler"/> for the distro OTLP exporter's HttpClient. The OpenTelemetry OTLP
/// metrics exporter exports on its reader thread via the <em>synchronous</em> <c>HttpClient.Send</c>, whose default
/// <c>DelegatingHandler.Send</c> walks <c>InnerHandler.Send</c> and so bypasses every handler that overrides only
/// <c>SendAsync</c> -- which is the agent's whole chain (<c>OtlpAuditHandler</c>, <c>CustomRetryHandler</c>), leaving
/// distro exports with no retry. This handler re-enters the async chain so retry (and the rest) run on the sync
/// export path too. It runs on the exporter's reader thread, never the application's, so blocking on the async
/// result here cannot deadlock an app thread.
/// <para>
/// It lives in the net8.0 plugin, not in Core: Core is a netstandard2.0 assembly, whose <c>HttpMessageHandler</c>
/// has no synchronous <c>Send</c> to override. The plugin injects this as the outermost handler via Core's
/// <c>DistroMetricsSettings.CreateHttpClient(DelegatingHandler)</c>. On .NET Framework (net462) there is no sync
/// <c>Send</c>, so this type is a transparent pass-through and the async chain is used unchanged.
/// </para>
/// </summary>
public sealed class SyncToAsyncHandler : DelegatingHandler
{
#if NET
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // base.SendAsync delegates to InnerHandler.SendAsync, so the whole async chain (audit, retry, transport)
        // runs. GetAwaiter().GetResult() unwraps the AggregateException so the inner exception/cancellation
        // propagates as-is rather than wrapped.
        //
        // Dependency: blocking on the async result here is safe only because this runs on the OTLP exporter's
        // reader thread, which has no SynchronizationContext. CustomRetryHandler (an agent-mode file this must not
        // edit) awaits its retry delays and inner sends without ConfigureAwait(false); on a thread that captured a
        // single-threaded context, those continuations would try to resume on the blocked thread and deadlock.
        // The reader thread captures no context, so they resume on the thread pool and complete. Do not reuse this
        // handler on a thread that has a SynchronizationContext.
        return base.SendAsync(request, cancellationToken).GetAwaiter().GetResult();
    }
#endif
}
