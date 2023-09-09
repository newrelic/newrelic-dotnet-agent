// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.XPath;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public static class XmlUtils
    {
        private class NamespaceAndName
        {
            public string Namespace = string.Empty;
            public string Name = string.Empty;
        }

        public static XmlDocument AddXmlNode(string filePath, string @namespace, IEnumerable<string> parentNodeNames, string nodeName, string value, bool saveOnCompletion = true, XmlDocument alteredDocument = null)
        {
            if (filePath == null)
                throw new ArgumentNullException("filePath");
            if (@namespace == null)
                throw new ArgumentNullException("namespace");
            if (parentNodeNames == null)
                throw new ArgumentNullException("parentNodeNames");
            if (nodeName == null)
                throw new ArgumentNullException("nodeName");
            if (value == null)
                throw new ArgumentNullException("value");

            var parentNodes = parentNodeNames
                .Where(parentNodeName => parentNodeName != null)
                .Select(parentNodeName => new NamespaceAndName { Namespace = @namespace, Name = parentNodeName });
            var leafNode = new NamespaceAndName { Namespace = @namespace, Name = nodeName };
            return AddXmlNode(filePath, parentNodes, leafNode, value, saveOnCompletion, alteredDocument);
        }

        public static XmlDocument AddXmlNode(string filePath, string @namespace, IEnumerable<string> parentNodeNames, string nodeName, string nodeValue, string attributeName, string attributeValue, bool saveOnCompletion = true, XmlDocument alteredDocument = null)
        {
            if (filePath == null)
                throw new ArgumentNullException("filePath");
            if (@namespace == null)
                throw new ArgumentNullException("namespace");
            if (parentNodeNames == null)
                throw new ArgumentNullException("parentNodeNames");
            if (nodeName == null)
                throw new ArgumentNullException("nodeName");
            if (nodeValue == null)
                throw new ArgumentNullException("nodeValue");
            if (attributeName == null)
                throw new ArgumentNullException("attributeName");
            if (attributeValue == null)
                throw new ArgumentNullException("attributeValue");

            var parentNodes = parentNodeNames
                .Where(parentNodeName => parentNodeName != null)
                .Select(parentNodeName => new NamespaceAndName { Namespace = @namespace, Name = parentNodeName });
            var leafNode = new NamespaceAndName { Namespace = @namespace, Name = nodeName };
            return AddXmlNode(filePath, parentNodes, leafNode, nodeValue, attributeName, attributeValue, saveOnCompletion, alteredDocument);
        }

        public static XmlDocument DeleteXmlNode(string filePath, string @namespace, IEnumerable<string> parentNodeNames, string nodeName, bool saveOnCompletion = true, XmlDocument alteredDocument = null)
        {

            var document = alteredDocument ?? new XmlDocument();
            if (alteredDocument == null)
                document.Load(filePath);

            var navigator = document.CreateNavigator();

            var parentNodes = parentNodeNames
                .Where(parentNodeName => parentNodeName != null)
                .Select(parentNodeName => new NamespaceAndName { Namespace = @namespace, Name = parentNodeName });

            var leafNode = new NamespaceAndName { Namespace = @namespace, Name = nodeName };

            foreach (var parentNode in parentNodes)
            {
                if (parentNode == null)
                    continue;

                if (!MoveToChildNode(navigator, parentNode))
                    return new XmlDocument();
            }

            if (!MoveToChildNode(navigator, leafNode))
                return new XmlDocument();

            navigator.DeleteSelf();

            if (saveOnCompletion) document.Save(filePath);

            return document;
        }

        public static XmlDocument ModifyOrCreateXmlNode(string filePath, string @namespace, IEnumerable<string> parentNodeNames, string nodeName, string value, bool saveOnCompletion = true, XmlDocument alteredDocument = null)
        {
            if (filePath == null)
                throw new ArgumentNullException("filePath");
            if (@namespace == null)
                throw new ArgumentNullException("namespace");
            if (parentNodeNames == null)
                throw new ArgumentNullException("parentNodeNames");
            if (nodeName == null)
                throw new ArgumentNullException("nodeName");
            if (value == null)
                throw new ArgumentNullException("value");

            var parentNodes = parentNodeNames
                .Where(parentNodeName => parentNodeName != null)
                .Select(parentNodeName => new NamespaceAndName { Namespace = @namespace, Name = parentNodeName });
            var attributeNode = new NamespaceAndName { Namespace = @namespace, Name = nodeName };
            return ModifyOrCreateXmlNode(filePath, parentNodes, attributeNode, value, saveOnCompletion, alteredDocument);
        }

        public static XmlDocument ModifyOrCreateXmlAttribute(string filePath, string @namespace, IEnumerable<string> parentNodeNames, string attributeName, string value, bool saveOnCompletion = true, XmlDocument alteredDocument = null)
        {
            var attributes = new[] { new KeyValuePair<string, string>(attributeName, value) };
            return ModifyOrCreateXmlAttributes(filePath, @namespace, parentNodeNames, attributes, saveOnCompletion, alteredDocument);
        }

        public static XmlDocument ModifyOrCreateXmlAttributes(string filePath, string @namespace, IEnumerable<string> parentNodeNames, IEnumerable<KeyValuePair<string, string>> attributes, bool saveOnCompletion = true, XmlDocument alteredDocument = null)
        {
            if (filePath == null)
                throw new ArgumentNullException("filePath");
            if (@namespace == null)
                throw new ArgumentNullException("namespace");
            if (parentNodeNames == null)
                throw new ArgumentNullException("parentNodeNames");
            if (attributes == null)
                throw new ArgumentNullException("attributes");

            var parentNodes = parentNodeNames
                .Where(parentNodeName => parentNodeName != null)
                .Select(parentNodeName => new NamespaceAndName { Namespace = @namespace, Name = parentNodeName });

            var attributeNodes = attributes
                .Where(attribute => attribute.Key != null)
                .Where(attribute => attribute.Value != null)
                .Select(attribute => new KeyValuePair<NamespaceAndName, string>(new NamespaceAndName { Name = attribute.Key }, attribute.Value));

            return ModifyOrCreateXmlAttributes(filePath, parentNodes, attributeNodes, saveOnCompletion, alteredDocument);
        }

        private static XmlDocument ModifyOrCreateXmlAttributes(string filePath, IEnumerable<NamespaceAndName> parentNodes, IEnumerable<KeyValuePair<NamespaceAndName, string>> attributeNodes, bool saveOnCompletion = true, XmlDocument alteredDocument = null)
        {
            if (filePath == null)
                throw new ArgumentNullException("filePath");
            if (parentNodes == null)
                throw new ArgumentNullException("parentNodes");
            if (attributeNodes == null)
                throw new ArgumentNullException("attributeNodes");

            var document = alteredDocument ?? new XmlDocument();
            if (alteredDocument == null)
                document.Load(filePath);

            var navigator = document.CreateNavigator();

            parentNodes
                .Where(parentNode => parentNode != null)
                .EnumerateAndExecuteAction(parentNode => MoveToOrCreateChildNode(navigator, parentNode));

            attributeNodes
                .Where(attributeNode => attributeNode.Key != null)
                .Where(attributeNode => attributeNode.Value != null)
                .EnumerateAndExecuteAction(attributeNode => SetOrCreateAttribute(navigator, attributeNode.Key, attributeNode.Value));

            if (saveOnCompletion) document.Save(filePath);

            return document;
        }

        public static XmlDocument DeleteXmlAttribute(string filePath, string @namespace, IEnumerable<string> parentNodeNames, string attributeName, bool saveOnCompletion = true, XmlDocument alteredDocument = null)
        {
            if (filePath == null)
                throw new ArgumentNullException("filePath");
            if (@namespace == null)
                throw new ArgumentNullException("namespace");
            if (parentNodeNames == null)
                throw new ArgumentNullException("parentNodeNames");
            if (attributeName == null)
                throw new ArgumentNullException("attributeName");

            var parentNodes = parentNodeNames
                .Where(parentNodeName => parentNodeName != null)
                .Select(parentNodeName => new NamespaceAndName { Namespace = @namespace, Name = parentNodeName });

            var attribute = new NamespaceAndName { Name = attributeName };

            return DeleteXmlAttribute(filePath, parentNodes, attribute, saveOnCompletion, alteredDocument);
        }

        private static XmlDocument DeleteXmlAttribute(string filePath, IEnumerable<NamespaceAndName> parentNodes, NamespaceAndName attribute, bool saveOnCompletion = true, XmlDocument alteredDocument = null)
        {
            if (filePath == null)
                throw new ArgumentNullException("filePath");
            if (parentNodes == null)
                throw new ArgumentNullException("parentNodes");
            if (attribute == null)
                throw new ArgumentNullException("attribute");

            var document = alteredDocument ?? new XmlDocument();
            if (alteredDocument == null)
                document.Load(filePath);

            var navigator = document.CreateNavigator();

            parentNodes
                .Where(parentNode => parentNode != null)
                .EnumerateAndExecuteAction(parentNode => MoveToOrCreateChildNode(navigator, parentNode));

            //don't save if nothing was done.
            if (navigator.GetAttribute(attribute.Name, attribute.Namespace) != string.Empty)
            {
                navigator.MoveToAttribute(attribute.Name, attribute.Namespace);
                navigator.DeleteSelf();
                if (saveOnCompletion) document.Save(filePath);
            }

            return document;
        }

        private static XmlDocument ModifyOrCreateXmlNode(string filePath, IEnumerable<NamespaceAndName> parentNodes, NamespaceAndName node, string value, bool saveOnCompletion = true, XmlDocument alteredDocument = null)
        {
            if (filePath == null)
                throw new ArgumentNullException("filePath");
            if (parentNodes == null)
                throw new ArgumentNullException("parentNodes");
            if (node == null)
                throw new ArgumentNullException("node");
            if (value == null)
                throw new ArgumentNullException("value");

            var document = alteredDocument ?? new XmlDocument();
            if (alteredDocument == null)
                document.Load(filePath);

            var navigator = document.CreateNavigator();

            parentNodes
                .Where(parentNode => parentNode != null)
                .ToList()
                .ForEach(parentNode => MoveToOrCreateChildNode(navigator, parentNode));

            SetOrCreateNode(navigator, node, value);

            if (saveOnCompletion) document.Save(filePath);

            return document;
        }

        private static XmlDocument AddXmlNode(string filePath, IEnumerable<NamespaceAndName> parentNodes, NamespaceAndName node, string value, bool saveOnCompletion = true, XmlDocument alteredDocument = null)
        {
            if (filePath == null)
                throw new ArgumentNullException("filePath");
            if (parentNodes == null)
                throw new ArgumentNullException("parentNodes");
            if (node == null)
                throw new ArgumentNullException("node");
            if (value == null)
                throw new ArgumentNullException("value");

            var document = alteredDocument ?? new XmlDocument();
            if (alteredDocument == null)
                document.Load(filePath);

            var navigator = document.CreateNavigator();

            parentNodes
                .Where(parentNode => parentNode != null)
                .ToList()
                .ForEach(parentNode => MoveToOrCreateChildNode(navigator, parentNode));

            navigator.AppendChildElement(string.Empty, node.Name, node.Namespace, value);

            if (saveOnCompletion) document.Save(filePath);

            return document;
        }

        private static XmlDocument AddXmlNode(string filePath, IEnumerable<NamespaceAndName> parentNodes, NamespaceAndName node, string nodeValue, string attributeName, string attributeValue, bool saveOnCompletion = true, XmlDocument alteredDocument = null)
        {
            if (filePath == null)
                throw new ArgumentNullException("filePath");
            if (parentNodes == null)
                throw new ArgumentNullException("parentNodes");
            if (node == null)
                throw new ArgumentNullException("node");
            if (nodeValue == null)
                throw new ArgumentNullException("nodeValue");
            if (attributeName == null)
                throw new ArgumentNullException("attributeName");
            if (attributeValue == null)
                throw new ArgumentNullException("attributeValue");

            var document = alteredDocument ?? new XmlDocument();
            if (alteredDocument == null)
                document.Load(filePath);

            var navigator = document.CreateNavigator();

            parentNodes
                .Where(parentNode => parentNode != null)
                .ToList()
                .ForEach(parentNode => MoveToOrCreateChildNode(navigator, parentNode));

            //relace the below with a way to add a single atttrib to thhe node....S

            var childDoc = new XmlDocument();
            var childElement = childDoc.CreateElement(node.Name);
            childElement.Attributes.Append(childDoc.CreateAttribute(attributeName));
            childElement.Attributes[attributeName].Value = attributeValue;
            childDoc.AppendChild(childElement);
            var childNav = childDoc.CreateNavigator();

            navigator.AppendChild(childDoc.InnerXml);



            if (saveOnCompletion) document.Save(filePath);

            return document;
        }

        private static void SetOrCreateAttribute(XPathNavigator navigator, NamespaceAndName attribute, string value)
        {
            if (navigator == null)
                throw new ArgumentNullException("navigator");
            if (attribute == null)
                throw new ArgumentNullException("attribute");
            if (value == null)
                throw new ArgumentNullException("value");

            var localNavigator = navigator.Clone();
            if (localNavigator.GetAttribute(attribute.Name, attribute.Namespace) != string.Empty)
            {
                localNavigator.MoveToAttribute(attribute.Name, attribute.Namespace);
                localNavigator.SetValue(value);
            }
            else
            {
                localNavigator.CreateAttribute(string.Empty, attribute.Name, attribute.Namespace, value);
            }
        }

        private static void SetOrCreateNode(XPathNavigator navigator, NamespaceAndName node, string value)
        {
            if (navigator == null)
                throw new ArgumentNullException("navigator");
            if (node == null)
                throw new ArgumentNullException("node");
            if (value == null)
                throw new ArgumentNullException("value");

            MoveToOrCreateChildNode(navigator, node);
            navigator.SetValue(value);
        }

        private static void MoveToOrCreateChildNode(XPathNavigator navigator, NamespaceAndName childNode)
        {
            if (navigator == null)
                throw new ArgumentNullException("navigator");
            if (childNode == null)
                throw new ArgumentNullException("childNode");
            if (childNode.Name == null)
                throw new NullReferenceException("childNode.Name");
            if (childNode.Namespace == null)
                throw new NullReferenceException("childNode.Namespace");

            if (MoveToChildNode(navigator, childNode))
                return;

            navigator.AppendChildElement(string.Empty, childNode.Name, childNode.Namespace, string.Empty);
            navigator.MoveToChild(childNode.Name, childNode.Namespace);
        }

        private static bool MoveToChildNode(XPathNavigator navigator, NamespaceAndName childNode)
        {
            if (navigator == null)
                throw new ArgumentNullException("navigator");
            if (childNode == null)
                throw new ArgumentNullException("childNode");
            if (childNode.Name == null)
                throw new NullReferenceException("childNode.Name");
            if (childNode.Namespace == null)
                throw new NullReferenceException("childNode.Namespace");

            var previousNavigator = navigator.Clone();
            navigator.MoveToChild(childNode.Name, childNode.Namespace);
            if (navigator.IsSamePosition(previousNavigator))
            {
                return false;
            }

            return true;
        }
    }

    internal static class EnumerableHelperExtensions
    {
        public static void EnumerateAndExecuteAction<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            foreach (var item in enumerable)
            {
                action(item);
            }
        }
    }
}
