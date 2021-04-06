// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Xml.Serialization;

namespace NewRelic.Agent.MultiverseScanner.ExtensionSerialization
{
    [XmlRoot(ElementName = "tracerFactory", Namespace = "urn:newrelic-extension")]
    public class TracerFactory
    {
        [XmlElement(ElementName = "match", Namespace = "urn:newrelic-extension")]
        public List<Match> Matches { get; set; }
    }
}
