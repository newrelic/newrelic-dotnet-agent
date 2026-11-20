// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>Lists and reads the NR instrumentation XML files the distro's XML toggles apply (see <see cref="DistroXmlToggles"/>).</summary>
public interface IDistroXmlFileSource
{
    /// <summary>The extensions folder itself (the other directories are its subfolders); null when there is none.</summary>
    string RootDirectory { get; }

    /// <summary>
    /// Whether <see cref="RootDirectory"/> exists now. When it does not, the XML toggles keep their current state instead of
    /// treating every shipped file as deleted.
    /// </summary>
    bool RootExists { get; }

    /// <summary>Every NR instrumentation XML file present now, without opening any of them.</summary>
    IReadOnlyList<DistroXmlFileEntry> List();

    /// <summary>
    /// Reads one listed file's bytes. A file that cannot be read is returned with null bytes and the reason in
    /// <see cref="DistroXmlFile.ReadError"/>; never throws.
    /// </summary>
    DistroXmlFile Read(DistroXmlFileEntry file);
}
