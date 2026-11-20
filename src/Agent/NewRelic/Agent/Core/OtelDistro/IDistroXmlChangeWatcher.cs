// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>Tells the distro's XML toggles (see <see cref="DistroXmlToggles"/>) that NR instrumentation XML changed on disk.</summary>
public interface IDistroXmlChangeWatcher : IDisposable
{
    /// <summary>
    /// Starts watching, possibly after this call returns (off the caller's thread); <paramref name="onChange"/> runs on the
    /// watcher's own thread after changes settle. Once the watcher is armed it calls <paramref name="changedSinceListing"/>
    /// and treats true as a change, so an edit made between the caller's last listing and arming is not lost. Only the
    /// first call starts anything, and a call after Dispose starts nothing.
    /// </summary>
    void Start(Action onChange, Func<bool> changedSinceListing);
}
