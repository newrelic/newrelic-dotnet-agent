// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public static class RandomPortGenerator
    {
        private const int minPortID = 50000;
        private const int maxPortID = 60000;
        private const int portPoolSize = maxPortID - minPortID;
        private const int maxAttempts = 200;
        private static readonly Random _randomNumberDiety;
        private static readonly HashSet<int> _usedPorts = new HashSet<int>();

        static RandomPortGenerator()
        {
            var seed = Process.GetCurrentProcess().Id + AppDomain.CurrentDomain.Id + Environment.TickCount;
            _randomNumberDiety = new Random(seed);
        }

        private static readonly object _usedPortLock = new object();
        public static int NextPort()
        {
            lock (_usedPortLock)
            {
                for (var countAttempts = 0; countAttempts < maxAttempts; countAttempts++)
                {
                    var potentialPort = _randomNumberDiety.Next(portPoolSize) + minPortID;
                    if (!_usedPorts.Contains(potentialPort) && IsPortAvailable(potentialPort))
                    {
                        _usedPorts.Add(potentialPort);
                        return potentialPort;
                    }
                }
            }

            throw new Exception($"Unable to obtain port after {maxAttempts} attempts.");
        }

        //Checks if something outside our current test run instance is currently using the port.
        //This does not prevent us from getting into a conflict with another process taking that port after this check,
        //but before the test app uses the assigned port.
        private static bool IsPortAvailable(int potentialPort)
        {
            try
            {
                if (!IsPortAvailableAccordingToOS(potentialPort))
                {
                    return false;
                }

                var tcp4Listener = new TcpListener(System.Net.IPAddress.Any, potentialPort);
                tcp4Listener.Start();
                tcp4Listener.Stop();

                var tcp6Listener = new TcpListener(System.Net.IPAddress.IPv6Any, potentialPort);
                tcp6Listener.Start();
                tcp6Listener.Stop();

                // Try to create a HTTP listener if we can, since this is what most test apps do.
                if (HttpListener.IsSupported)
                {
                    HttpListener listener = new HttpListener();
                    listener.Prefixes.Add($"http://127.0.0.1:{potentialPort}/port-check-{Guid.NewGuid()}/");
                    listener.Start();
                    listener.Stop();
                }

                return WaitUntilPortIsReportedAvailable(potentialPort);
            }
            catch (Exception) { }
            return false;
        }

        private static bool IsPortAvailableAccordingToOS(int port)
        {
            var activeListeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            if (activeListeners.Any(x => x.Port == port))
            {
                return false;
            }

            var activeConnections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
            if (activeConnections.Any(x => x.LocalEndPoint.Port == port))
            {
                return false;
            }

            return true;
        }

        private static bool WaitUntilPortIsReportedAvailable(int port, int waitSecond = 5)
        {
            for (var waitDeadline = DateTime.Now + TimeSpan.FromSeconds(waitSecond); DateTime.Now < waitDeadline; Thread.Sleep(100))
            {
                if (IsPortAvailableAccordingToOS(port))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool TryReleasePort(int port)
        {
            lock (_usedPortLock)
            {
                _usedPorts.Remove(port);
            }
            return true;
        }
    }
}
