// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Xml.Serialization;

namespace NewRelic.Agent.MultiverseScanner.ExtensionSerialization
{
    [XmlRoot(ElementName = "instrumentation", Namespace = "urn:newrelic-extension")]
    public class Instrumentation
    {
        [XmlElement(ElementName = "tracerFactory", Namespace = "urn:newrelic-extension")]
        public List<TracerFactory> TracerFactories { get; set; }
    }
}
