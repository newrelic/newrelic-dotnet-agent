// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Exceptions;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Core.Logging;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.DataTransport
{
    /// <summary>
    /// The <see cref="ConnectionManager"/> understands the business logic of *when* to connect, disconnect, send data, etc. It is the companion of <see cref="ConnectionHandler"/> which knows *how* to connect, disconnect, etc.
    /// 
    /// The main purpose of the <see cref="ConnectionManager"/> is to ensure that <see cref="ConnectionHandler"/> is used a thread-safe manner. It also listens for events such as `RestartAgentEvent` to trigger reconnects. All calls into <see cref="ConnectionHandler"/> are synchronized with locks.
    /// </summary>
    public class ConnectionManager : ConfigurationBasedService, IConnectionManager
    {
        private static readonly TimeSpan[] ConnectionRetryBackoffSequence = new[]
        {
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(60),
            TimeSpan.FromSeconds(120),
            TimeSpan.FromSeconds(300)
        };

        private readonly IConnectionHandler _connectionHandler;
        private readonly IScheduler _scheduler;
        private int _connectionAttempt = 0;
        private bool _started;
        private readonly SemaphoreSlim _syncSemaphore = new SemaphoreSlim(1);

        public ConnectionManager(IConnectionHandler connectionHandler, IScheduler scheduler)
        {
            _connectionHandler = connectionHandler;
            _scheduler = scheduler;

            _subscriptions.Add<StartAgentEvent>(OnStartAgentAsyncEventHandler);
            _subscriptions.Add<RestartAgentEvent>(OnRestartAgentAsyncEventHandler);

            // calling Disconnect on Shutdown is crashing on Linux.  This is probably a CLR bug, but we have to work around it.
            // The Shutdown call is actually not very important (agent runs time out after 5 minutes anyway) so just don't call it.
#if NETFRAMEWORK
			_subscriptions.Add<CleanShutdownEvent>(OnCleanShutdownAsyncEventHandler);
#endif
        }

        public async Task AttemptAutoStartAsync()
        {
            if (_configuration.AutoStartAgent)
                await StartAsync();
        }

        #region Synchronized methods

        private async Task StartAsync()
        {
            // First, a quick happy path check which won't force callers to wait around to find out that we've already started a long time ago
            if (_started)
                return;

            await _syncSemaphore.WaitAsync();
            try
            {
                // Second, a thread-safe check inside the blocking code block that ensures we'll never start more than once
                if (_started)
                    return;

                if (_configuration.CollectorSyncStartup || _configuration.CollectorSendDataOnExit)
                    await ConnectAsync();
                else
                    _scheduler.ExecuteOnce(ConnectAsyncEventHandler, TimeSpan.Zero);

                _started = true;
            }
            finally
            {
                _syncSemaphore.Release();   
            }
        }

        private async void ConnectAsyncEventHandler() => await ConnectAsync();

        private async Task ConnectAsync()
        {
            try
            {
                await _syncSemaphore.WaitAsync();

                try
                {
                    await _connectionHandler.ConnectAsync();
                }
                finally
                {
                    _syncSemaphore.Release();
                }

                _connectionAttempt = 0;
            }
            // This exception is thrown when the agent receives an unexpected HTTP error
            // This is also the parent type of some of the more specific HTTP errors that we handle
            catch (HttpException ex)
            {
                HandleHttpErrorResponse(ex);
            }
            // Occurs when the agent connects to APM but the connection gets aborted by the collector
            catch (SocketException)
            {
                ScheduleRestart();
            }
            // Occurs when the agent is unable to read data from the transport connection (this might occur when a socket exception happens - in that case the exception will be caught above)
            catch (IOException)
            {
                ScheduleRestart();
            }
            // Occurs when no network connection is available, DNS unavailable, etc.
            catch (WebException)
            {
                ScheduleRestart();
            }
            // Usually occurs when a request times out but did not get far enough along to trigger a timeout exception
            catch (OperationCanceledException)
            {
                ScheduleRestart();
            }
            // This catch all is in place so that we avoid doing harm for all of the potentially destructive things that could happen during a connect.
            // We want to error on the side of doing no harm to our customers
            catch (Exception ex)
            {
                ImmediateShutdown(ex.Message);
            }
        }

        private async Task DisconnectAsync()
        {
            await _syncSemaphore.WaitAsync();
            try
            {
                await _connectionHandler.DisconnectAsync();
            }
            finally
            {
                _syncSemaphore.Release();
            }
        }

        private async Task ReconnectAsync()
        {
            EventBus<StopHarvestEvent>.Publish(new StopHarvestEvent());

            await _syncSemaphore.WaitAsync();
            try
            {
                await DisconnectAsync();
                await ConnectAsync();
            }
            finally
            {
                _syncSemaphore.Release();
            }
        }

        private async void ReconnectAsyncEventHandler() => await ReconnectAsync();

        public async Task<T> SendDataRequestAsync<T>(string method, params object[] data)
        {
            await _syncSemaphore.WaitAsync();
            try
            {
                return await _connectionHandler.SendDataRequestAsync<T>(method, data);
            }
            finally
            {
                _syncSemaphore.Release();
            }
        }

        #endregion Synchronized methods

        #region Helper methods

        private static void ImmediateShutdown(string message)
        {
            Log.InfoFormat("Shutting down: {0}", message);
            EventBus<KillAgentEvent>.Publish(new KillAgentEvent());
        }

        private void ScheduleRestart()
        {
            var _retryTime = ConnectionRetryBackoffSequence[_connectionAttempt];
            Log.InfoFormat("Will attempt to reconnect in {0} seconds", _retryTime.TotalSeconds);
            _scheduler.ExecuteOnce(ConnectAsyncEventHandler, _retryTime);

            _connectionAttempt = Math.Min(_connectionAttempt + 1, ConnectionRetryBackoffSequence.Length - 1);
        }

        private void HandleHttpErrorResponse(HttpException ex)
        {
            switch (ex.StatusCode)
            {
                case HttpStatusCode.Gone:
                    ImmediateShutdown(ex.Message);
                    break;
                default:
                    ScheduleRestart();
                    break;
            }
        }

        #endregion

        #region Event handlers

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // If we receive a non-server config update while connected then we need to reconnect.
            // Receiving a server config update implies that we just connected or disconnected so there's no need to do anything.
            if (configurationUpdateSource == ConfigurationUpdateSource.Server)
                return;
            if (_configuration.AgentRunId == null)
                return;

            Log.Info("Reconnecting due to configuration change");

            _scheduler.ExecuteOnce(ReconnectAsyncEventHandler, TimeSpan.Zero);
        }

        private async Task OnStartAgentAsync(StartAgentEvent eventData) => await StartAsync();

        private async void OnStartAgentAsyncEventHandler(StartAgentEvent eventData) => await OnStartAgentAsync(eventData);

        private async void OnRestartAgentAsyncEventHandler(RestartAgentEvent eventData) => await ReconnectAsync();

        private async void OnCleanShutdownAsyncEventHandler(CleanShutdownEvent eventData) => await DisconnectAsync();

        #endregion Event handlers
    }
}
