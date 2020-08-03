// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;
using NewRelic.Agent.IntegrationTestHelpers.Collections.Generic;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public static class CommonUtils
    {
        private class NamespaceAndName
        {
            public string Namespace = string.Empty;
            public string Name = string.Empty;
        }

        public static void CopyDirectory(string sourceDirectoryPath, string destinationDirectoryPath, string searchPattern = "*")
        {
            var sourceDirectory = new DirectoryInfo(sourceDirectoryPath);

            if (!sourceDirectory.Exists)
                throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirectoryPath);

            if (!Directory.Exists(destinationDirectoryPath))
                Directory.CreateDirectory(destinationDirectoryPath);

            foreach (var file in sourceDirectory.EnumerateFiles(searchPattern))
            {
                if (file == null)
                    continue;
                var destinationFilePath = Path.Combine(destinationDirectoryPath, file.Name);
                file.CopyTo(destinationFilePath, true);
            }

            foreach (var subdirectory in sourceDirectory.EnumerateDirectories())
            {
                if (subdirectory == null)
                    continue;
                // When we run on the test server, we always get a clean Logs directory; but when we run
                // on a developer machine, the Logs directory may have content, so don't copy it over.
                // (On a development machine, the Extensions directory of the New Relic Home may contain
                // custom instrumentation, too, which can get copied over and cause confusing test results;
                // but there's no easy solution to this one, so ... don't do that.)
                if (subdirectory.Name == "Logs")
                    continue;
                var destinationSubdirectoryPath = Path.Combine(destinationDirectoryPath, subdirectory.Name);
                CopyDirectory(subdirectory.FullName, destinationSubdirectoryPath, searchPattern);
            }
        }

        /// <summary>
        /// Sets an attribute on a tracerFactory xml element in a New Relic instrumentation file.
        /// </summary>
        /// <param name="filePath">Fully-qualified pathname of the instrumentation file.</param>
        /// <param name="tracerFactoryName">String to match on the name attribute of a tracerFactory element. If the argument is an empty string, all tracerFactory elements will be matched.</param>
        /// <param name="attributeName">The xml attribute name to set.</param>
        /// <param name="value">The value to apply to the xml attribute.</param>
        /// <exception cref="System.ArgumentNullException">If any of the argument values are null.</exception>
        public static void SetAttributeOnTracerFactoryInNewRelicInstrumentation(string filePath, string tracerFactoryName, string attributeName, string value)
        {
            if (filePath == null)
                throw new ArgumentNullException("filePath");
            if (attributeName == null)
                throw new ArgumentNullException("attributeName");
            if (value == null)
                throw new ArgumentNullException("value");

            var document = new XmlDocument();
            document.Load(filePath);
            var navigator = document.CreateNavigator();
            var xmlnsManager = new XmlNamespaceManager(navigator.NameTable);
            xmlnsManager.AddNamespace("x", "urn:newrelic-extension");

            string xPath;
            if (tracerFactoryName == string.Empty)
            {
                xPath = "//x:tracerFactory";
            }
            else
            {
                xPath = string.Format("//x:tracerFactory[@name='{0}']", tracerFactoryName);
            }

            var xPathExp = XPathExpression.Compile(xPath);
            xPathExp.SetContext(xmlnsManager);

            XPathNodeIterator iterator = navigator.Select(xPathExp);
            while (iterator.MoveNext())
            {
                var node = iterator.Current;

                if (node.GetAttribute(attributeName, xmlnsManager.DefaultNamespace) != string.Empty)
                {
                    node.MoveToAttribute(attributeName, xmlnsManager.DefaultNamespace);
                    node.SetValue(value);
                }
                else
                {
                    node.CreateAttribute(string.Empty, attributeName, xmlnsManager.DefaultNamespace, value);
                }
            }

            document.Save(filePath);
        }

        public static void ModifyOrCreateXmlAttributeInNewRelicConfig(string filePath, IEnumerable<string> parentNodeNames, string attributeName, string value)
        {
            ModifyOrCreateXmlAttribute(filePath, "urn:newrelic-config", parentNodeNames, attributeName, value);
        }

        public static void ModifyOrCreateXmlAttributesInNewRelicConfig(string filePath, IEnumerable<string> parentNodeNames, IEnumerable<KeyValuePair<string, string>> attributes)
        {
            ModifyOrCreateXmlAttributes(filePath, "urn:newrelic-config", parentNodeNames, attributes);
        }

        public static void ModifyOrCreateXmlNodeInNewRelicConfig(string filePath, IEnumerable<string> parentNodeNames, string nodeName, string value)
        {
            ModifyOrCreateXmlNode(filePath, "urn:newrelic-config", parentNodeNames, nodeName, value);
        }

        public static void AddXmlNodeInNewRelicConfig(string filePath, IEnumerable<string> parentNodeNames, string nodeName, string value)
        {
            AddXmlNode(filePath, "urn:newrelic-config", parentNodeNames, nodeName, value);
        }

        public static void DeleteXmlNodeFromNewRelicConfig(string filePath, IEnumerable<string> parentNodeNames, string nodeName)
        {
            DeleteXmlNode(filePath, "urn:newrelic-config", parentNodeNames, nodeName);
        }

        public static void SetAppNameInAppConfig(string filePath, string applicationName)
        {
            SetAppConfigAppSetting(filePath, "NewRelic.AppName", applicationName);
        }

        public static void SetAppConfigAppSetting(string filePath, string key, string value)
        {
            var document = new XmlDocument();
            document.Load(filePath);

            var configurationNode = document.SelectSingleNode("/configuration");
            if (configurationNode == null)
            {
                throw new InvalidOperationException($"Invalid configuration file. Missing <configuration> element. File: {filePath}");
            }

            var appSettingsNode = configurationNode.SelectSingleNode("appSettings");

            if (appSettingsNode == null)
            {
                appSettingsNode = document.CreateElement("appSettings");
                configurationNode.AppendChild(appSettingsNode);
            }

            var settingElement = document.CreateElement("add");
            settingElement.SetAttribute("key", key);
            settingElement.SetAttribute("value", value);

            appSettingsNode.AppendChild(settingElement);

            document.Save(filePath);
        }

        public static void SetNewRelicAppSetting(string filePath, string key, string value)
        {
            SetAppSetting(filePath, "urn:newrelic-config", key, value);
        }

        public static void SetAppSetting(string filePath, string @namespace, string key, string value)
        {
            var parentNodes = new[] { "configuration", "appSettings", "add" };
            var attributes = new[]
            {
                new KeyValuePair<string, string>("key", key),
                new KeyValuePair<string, string>("value", value)
            };
            ModifyOrCreateXmlAttributes(filePath, @namespace, parentNodes, attributes);
        }

        public static void AddXmlNode(string filePath, string @namespace, IEnumerable<string> parentNodeNames, string nodeName, string value)
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
            AddXmlNode(filePath, parentNodes, leafNode, value);
        }

        public static void DeleteXmlNode(string filePath, string @namespace, IEnumerable<string> parentNodeNames, string nodeName)
        {
            var document = new XmlDocument();
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
                    return;
            }

            if (!MoveToChildNode(navigator, leafNode))
                return;

            navigator.DeleteSelf();

            document.Save(filePath);
        }

        public static void ModifyOrCreateXmlNode(string filePath, string @namespace, IEnumerable<string> parentNodeNames, string nodeName, string value)
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
            ModifyOrCreateXmlNode(filePath, parentNodes, attributeNode, value);
        }

        public static void ModifyOrCreateXmlAttribute(string filePath, string @namespace, IEnumerable<string> parentNodeNames, string attributeName, string value)
        {
            var attributes = new[] { new KeyValuePair<string, string>(attributeName, value) };
            ModifyOrCreateXmlAttributes(filePath, @namespace, parentNodeNames, attributes);
        }

        public static void ModifyOrCreateXmlAttributes(string filePath, string @namespace, IEnumerable<string> parentNodeNames, IEnumerable<KeyValuePair<string, string>> attributes)
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

            ModifyOrCreateXmlAttributes(filePath, parentNodes, attributeNodes);
        }

        private static void ModifyOrCreateXmlAttributes(string filePath, IEnumerable<NamespaceAndName> parentNodes, IEnumerable<KeyValuePair<NamespaceAndName, string>> attributeNodes)
        {
            if (filePath == null)
                throw new ArgumentNullException("filePath");
            if (parentNodes == null)
                throw new ArgumentNullException("parentNodes");
            if (attributeNodes == null)
                throw new ArgumentNullException("attributeNodes");

            var document = new XmlDocument();
            document.Load(filePath);
            var navigator = document.CreateNavigator();

            parentNodes
                .Where(parentNode => parentNode != null)
                .ForEachNow(parentNode => MoveToOrCreateChildNode(navigator, parentNode));

            attributeNodes
                .Where(attributeNode => attributeNode.Key != null)
                .Where(attributeNode => attributeNode.Value != null)
                .ForEachNow(attributeNode => SetOrCreateAttribute(navigator, attributeNode.Key, attributeNode.Value));

            document.Save(filePath);
        }

        private static void ModifyOrCreateXmlNode(string filePath, IEnumerable<NamespaceAndName> parentNodes, NamespaceAndName node, string value)
        {
            if (filePath == null)
                throw new ArgumentNullException("filePath");
            if (parentNodes == null)
                throw new ArgumentNullException("parentNodes");
            if (node == null)
                throw new ArgumentNullException("node");
            if (value == null)
                throw new ArgumentNullException("value");

            var document = new XmlDocument();
            document.Load(filePath);
            var navigator = document.CreateNavigator();

            parentNodes
                .Where(parentNode => parentNode != null)
                .ToList()
                .ForEach(parentNode => MoveToOrCreateChildNode(navigator, parentNode));

            SetOrCreateNode(navigator, node, value);

            document.Save(filePath);
        }

        private static void AddXmlNode(string filePath, IEnumerable<NamespaceAndName> parentNodes, NamespaceAndName node, string value)
        {
            if (filePath == null)
                throw new ArgumentNullException("filePath");
            if (parentNodes == null)
                throw new ArgumentNullException("parentNodes");
            if (node == null)
                throw new ArgumentNullException("node");
            if (value == null)
                throw new ArgumentNullException("value");

            var document = new XmlDocument();
            document.Load(filePath);
            var navigator = document.CreateNavigator();

            parentNodes
                .Where(parentNode => parentNode != null)
                .ToList()
                .ForEach(parentNode => MoveToOrCreateChildNode(navigator, parentNode));

            navigator.AppendChildElement(string.Empty, node.Name, node.Namespace, value);

            document.Save(filePath);
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

        public static string GetLocalPathFromRemotePath(string remotePath)
        {
            var match = Regex.Match(remotePath, @"\\\\.*?\\(.)\$\\(.*)");
            var driveLetter = match.Groups[1];
            var path = match.Groups[2];
            return string.Format(@"{0}:\{1}", driveLetter, path);
        }

        public static void AddCustomInstrumentation(string instrumentationFilePath, string assemblyName, string typeName, string methodName, string wrapperName = null, string metricName = null, int? transactionNamingPriority = null)
        {
            const string instrumentationNamespace = "urn:newrelic-extension";

            if (!File.Exists(instrumentationFilePath))
                CreateEmptyInstrumentationFile(instrumentationFilePath);

            var document = new XmlDocument();
            document.Load(instrumentationFilePath);
            var navigator = document.CreateNavigator();
            var xmlnsManager = new XmlNamespaceManager(navigator.NameTable);
            xmlnsManager.AddNamespace("x", instrumentationNamespace);

            var extensionNode = document.ChildNodes[1];
            Assert.Equal("extension", extensionNode.Name);

            var instrumentationNode = extensionNode.ChildNodes[0];
            Assert.Equal("instrumentation", instrumentationNode.Name);

            var tracerFactoryNode = instrumentationNode.AppendChild(document.CreateNode(XmlNodeType.Element, "tracerFactory", instrumentationNamespace));
            if (!string.IsNullOrEmpty(wrapperName))
            {
                var tracerFactoryNameAttribute = tracerFactoryNode.Attributes.Append(document.CreateAttribute("name"));
                tracerFactoryNameAttribute.Value = wrapperName;
            }

            if (!string.IsNullOrEmpty(metricName))
            {
                var tracerMetricNameAttribute = tracerFactoryNode.Attributes.Append(document.CreateAttribute("metricName"));
                tracerMetricNameAttribute.Value = metricName;
            }

            if (transactionNamingPriority.HasValue)
            {
                var transactionNamingPriorityAttribute = tracerFactoryNode.Attributes.Append(document.CreateAttribute("transactionNamingPriority"));
                transactionNamingPriorityAttribute.Value = transactionNamingPriority.ToString();
            }


            var matchNode = tracerFactoryNode.AppendChild(document.CreateNode(XmlNodeType.Element, "match", instrumentationNamespace));
            var assemblyNameAttribute = matchNode.Attributes.Append(document.CreateAttribute("assemblyName"));
            assemblyNameAttribute.Value = assemblyName;
            var classNameAttribute = matchNode.Attributes.Append(document.CreateAttribute("className"));
            classNameAttribute.Value = typeName;
            var exactMethodMatcherNode = matchNode.AppendChild(document.CreateNode(XmlNodeType.Element, "exactMethodMatcher", instrumentationNamespace));
            var methodNameAttribute = exactMethodMatcherNode.Attributes.Append(document.CreateAttribute("methodName"));
            methodNameAttribute.Value = methodName;

            document.Save(instrumentationFilePath);

            var inst = File.ReadAllText(instrumentationFilePath);
        }

        private static void CreateEmptyInstrumentationFile(string instrumentationFilePath)
        {
            const string emptyInstrumentationFileText = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <extension xmlns=""urn:newrelic-extension"">
                <instrumentation>
                </instrumentation>
            </extension>";

            File.WriteAllText(instrumentationFilePath, emptyInstrumentationFileText);
        }
    }

    public static class EnumerableExtensions
    {
        public static IEnumerable<T> AsEnumerable<T>(this T item)
        {
            if (item == null)
                return Enumerable.Empty<T>();

            return item.AsEnumerableInternal();
        }

        private static IEnumerable<T> AsEnumerableInternal<T>(this T item)
        {
            yield return item;
        }
    }
}
