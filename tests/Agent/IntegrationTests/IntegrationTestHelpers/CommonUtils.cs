// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.XPath;
using NewRelic.Agent.IntegrationTestHelpers.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers.Models;
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

        public static void DeleteFile(string filePath, TimeSpan? timeoutOrZero = null)
        {
            if (filePath == null)
                throw new ArgumentNullException("filePath");

            var timeout = timeoutOrZero ?? TimeSpan.Zero;

            var timeTaken = Stopwatch.StartNew();
            do
            {
                if (!CommonUtils.IsFileLocked(filePath))
                {
                    File.Delete(filePath);
                    return;
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(100));
            } while (timeTaken.Elapsed < timeout);
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

        public static void SetAppNameInAppConfig(string filePath, string applicationName, string @namespace = null)
        {
            SetConfigAppSetting(filePath, "NewRelic.AppName", applicationName, @namespace);
        }

        public static void SetConfigAppSetting(string filePath, string key, string value, string @namespace)
        {
            var document = new XmlDocument();
            document.Load(filePath);

            var configurationNode = document.DocumentElement;
            if (configurationNode == null)
            {
                throw new InvalidOperationException($"Invalid configuration file. Missing <configuration> element. File: {filePath}");
            }

            var appSettingsNode = configurationNode.SelectSingleNode("appSettings");

            if (appSettingsNode == null)
            {
                appSettingsNode = document.CreateElement("appSettings", @namespace);
                configurationNode.AppendChild(appSettingsNode);
            }

            var settingElement = document.CreateElement("add", @namespace);
            settingElement.SetAttribute("key", key);
            settingElement.SetAttribute("value", value);

            appSettingsNode.AppendChild(settingElement);

            document.Save(filePath);
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
                .ForEachNow(parentNode => MoveToOrCreateChildNode(navigator, parentNode));

            attributeNodes
                .Where(attributeNode => attributeNode.Key != null)
                .Where(attributeNode => attributeNode.Value != null)
                .ForEachNow(attributeNode => SetOrCreateAttribute(navigator, attributeNode.Key, attributeNode.Value));

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
                .ForEachNow(parentNode => MoveToOrCreateChildNode(navigator, parentNode));

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

        public static string GetLocalPathFromRemotePath(string remotePath)
        {
            var match = Regex.Match(remotePath, @"\\\\.*?\\(.)\$\\(.*)");
            var driveLetter = match.Groups[1];
            var path = match.Groups[2];
            return string.Format(@"{0}:\{1}", driveLetter, path);
        }

        public static XmlDocument AddCustomInstrumentation(string instrumentationFilePath, string assemblyName, string typeName, string methodName, string wrapperName = null, string metricName = null, int? transactionNamingPriority = null, bool saveOnCompletion = true, XmlDocument alteredDocument = null)
        {
            const string instrumentationNamespace = "urn:newrelic-extension";

            if (!File.Exists(instrumentationFilePath))
                CreateEmptyInstrumentationFile(instrumentationFilePath);

            var document = alteredDocument ?? new XmlDocument();
            if (alteredDocument == null)
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

            if (saveOnCompletion) document.Save(instrumentationFilePath);

            return document;
        }

        public static void CreateEmptyInstrumentationFile(string instrumentationFilePath)
        {
            const string emptyInstrumentationFileText = @"<?xml version=""1.0"" encoding=""utf-8""?>
			<extension xmlns=""urn:newrelic-extension"">
				<instrumentation>
				</instrumentation>
			</extension>";

            File.WriteAllText(instrumentationFilePath, emptyInstrumentationFileText);
        }

        public static string GetAgentLogFileNameFromNewRelicConfig(string newRelicConfigurationFilePath)
        {
            XmlDocument document = new XmlDocument();
            document.Load(newRelicConfigurationFilePath);
            var mgr = new XmlNamespaceManager(document.NameTable);
            mgr.AddNamespace("x", "urn:newrelic-extension");

            var navigator = document.CreateNavigator();

            return GetAttributeValue(navigator, "urn:newrelic-config", new[] { "configuration", "log" }, "fileName");
        }

        public static string GetAgentLogFileDirectoryPath(string newRelicConfigurationFilePath)
        {
            XmlDocument document = new XmlDocument();
            document.Load(newRelicConfigurationFilePath);
            var mgr = new XmlNamespaceManager(document.NameTable);
            mgr.AddNamespace("x", "urn:newrelic-extension");

            var navigator = document.CreateNavigator();

            return GetAttributeValue(navigator, "urn:newrelic-config", new[] { "configuration", "log" }, "directory");
        }

        private static string GetAttributeValue(XPathNavigator navigator, string @namespace, IEnumerable<string> parentNodes, string attributeName)
        {
            foreach (var parentNode in parentNodes)
            {
                navigator.MoveToChild(parentNode, @namespace);
            }

            navigator.MoveToAttribute(attributeName, "");

            return navigator.Value;
        }


        public static void MoveFile(string originalPath, string destinationPath, TimeSpan? timeoutOrZero = null)
        {
            if (!File.Exists(originalPath))
                throw new FileNotFoundException();

            var timeout = timeoutOrZero ?? TimeSpan.Zero;

            var timeTaken = Stopwatch.StartNew();
            do
            {
                if (!CommonUtils.IsFileLocked(originalPath))
                {
                    File.Move(originalPath, destinationPath);
                    return;
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(100));
            } while (timeTaken.Elapsed < timeout);

        }

        public static void RenameFile(string originalPath, string newPath, TimeSpan? timeoutOrZero = null)
        {
            MoveFile(originalPath, newPath, timeoutOrZero);
        }

        public static string GetRandomString()
        {
            return Path.GetRandomFileName().Replace(".", "");
        }

        public static string GetInstrumentationChangeDetectedFragment(Match match)
        {
            return match.Success ? match.ToString().Split(':')[4].Trim() : string.Empty;
        }

        public static string GetInstrumentationChangeDetectedAction(string fragment)
        {
            return string.IsNullOrEmpty(fragment) ? string.Empty : fragment.Substring(0, 7);
        }

        public static string GetInstrumentationChangeDetectedFilePath(string fragment)
        {
            return string.IsNullOrEmpty(fragment) ? string.Empty : fragment.Substring(10).Trim().ToLowerInvariant();
        }

        public static List<Metric> GetMetrics(AgentLogFile agentLogFile, TimeSpan? timeoutOrZero = null)
        {
            var metrics = new List<Metric>();
            var timeout = timeoutOrZero ?? TimeSpan.FromSeconds(5);
            var timeTaken = Stopwatch.StartNew();
            do
            {
                metrics = agentLogFile.GetMetrics().ToList();
                if (metrics.Count > 0) return metrics;
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
            } while (timeTaken.Elapsed < timeout);

            return metrics;
        }

        public static bool IsFileLocked(string filePath)
        {
            FileStream stream = null;

            try
            {
                var file = new FileInfo(filePath);
                stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            return false;
        }

        public static string NormalizeHostname(string host)
        {
            var resolvedHostName = IsLocalHost(host) ? Dns.GetHostName() : host;
            return resolvedHostName;
        }

        private static bool IsLocalHost(string host)
        {
            var localhost = new[] { ".", "localhost" };
            var hostIsLocalhost = localhost.Contains(host);
            if (!hostIsLocalhost)
            {
                IPAddress ipAddress;
                var isIpAddress = IPAddress.TryParse(host, out ipAddress);
                hostIsLocalhost = isIpAddress && IPAddress.IsLoopback(ipAddress);
            }
            return hostIsLocalhost;
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
