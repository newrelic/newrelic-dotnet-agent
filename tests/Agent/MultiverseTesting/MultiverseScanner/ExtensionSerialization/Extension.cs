// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Xml.Serialization;

namespace NewRelic.Agent.MultiverseScanner.ExtensionSerialization
{
    [XmlRoot(ElementName = "extension", Namespace = "urn:newrelic-extension")]
    public class Extension
    {
        [XmlElement(ElementName = "instrumentation", Namespace = "urn:newrelic-extension")]
        public Instrumentation Instrumentation { get; set; }

        [XmlAttribute(AttributeName = "xmlns", Namespace = "urn:newrelic-extension")]
        public string Xmlns { get; set; }
    }

    public class InstXML
    {
        [XmlElement(ElementName = "extension", Namespace = "urn:newrelic-extension")]
        public Extension Extension { get; set; }

        [XmlAttribute(AttributeName = "xmlns", Namespace = "urn:newrelic-extension")]
        public string Xmlns { get; set; }
    }
}
