// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Xml.Serialization;

namespace NewRelic.Agent.MultiverseScanner.ExtensionSerialization
{
    [XmlRoot(ElementName = "match", Namespace = "urn:newrelic-extension")]
    public class Match
    {
        [XmlElement(ElementName = "exactMethodMatcher", Namespace = "urn:newrelic-extension")]
        public List<ExactMethodMatcher> ExactMethodMatchers { get; set; }

        [XmlAttribute(AttributeName = "assemblyName")]
        public string AssemblyName { get; set; }

        [XmlAttribute(AttributeName = "className")]
        public string ClassName { get; set; }
    }
}
