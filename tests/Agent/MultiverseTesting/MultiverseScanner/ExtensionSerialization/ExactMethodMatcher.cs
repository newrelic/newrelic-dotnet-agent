// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Xml.Serialization;

namespace NewRelic.Agent.MultiverseScanner.ExtensionSerialization
{
    [XmlRoot(ElementName = "exactMethodMatcher", Namespace = "urn:newrelic-extension")]
    public class ExactMethodMatcher
    {
        [XmlAttribute(AttributeName = "methodName")]
        public string MethodName { get; set; }

        [XmlAttribute(AttributeName = "parameters")]
        public string Parameters { get; set; }

        [XmlIgnore]
        public string MethodSignature { get { return $"{MethodName}({Parameters ?? string.Empty})"; } }
    }
}
