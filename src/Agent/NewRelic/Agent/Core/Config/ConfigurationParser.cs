// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core;
using NewRelic.Core.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace NewRelic.Agent.Core.Config
{

    /// <summary>
    /// The ConfigurationParser populates configuration objects with dictionaries of key/value pairs.
    /// It uses the ConfigurationAttribute annotations on the configuration object to define the key mappings.
    /// </summary>
    public class ConfigurationParser
    {
        private delegate void ParseConfigurationValue(object value);
        private readonly IDictionary<string, ParseConfigurationValue> parsers;

        /// <summary>
        /// Creates a configuration parser with the given configuration object by inspecting the object's
        /// properties searching for the ConfigurationAttribute marker.
        /// </summary>
        /// <param name="configurationObject"></param>
        public ConfigurationParser(object configurationObject)
        {
            this.parsers = new Dictionary<string, ParseConfigurationValue>();
            foreach (PropertyInfo prop in configurationObject.GetType().GetProperties())
            {
                object[] attr = prop.GetCustomAttributes(typeof(ConfigurationAttribute), false);
                if (attr.Length > 0)
                {
                    var configAttr = attr[0] as ConfigurationAttribute;
                    string key = configAttr.Key;

                    ParseConfigurationValue parser = GetParseConfigurationValue(configurationObject, configAttr, prop);
                    if (parser != null)
                    {
                        parsers.Add(key, parser);
                    }
                }
            }
        }

        private static ParseConfigurationValue GetParseConfigurationValue(object configurationObject, ConfigurationAttribute configAttr, PropertyInfo prop)
        {
            MethodInfo method = configurationObject.GetType().GetMethod("Set" + prop.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (method != null)
            {
                return new SetterParser(configurationObject, method).Set;
            }

            try
            {
                if (prop.PropertyType.IsGenericType)
                {
                    Type type = prop.PropertyType.GetGenericTypeDefinition();
                    if (typeof(ICollection<>).IsAssignableFrom(type))
                    {
                        return new CollectionPropertyParser(configurationObject, prop).SetList;
                    }
                }

                return new PropertyParser(configurationObject, prop).Set;
            }
            catch (Exception ex)
            {
                StringBuilder msg = new StringBuilder(ex.Message);
                if (ex.InnerException != null)
                {
                    msg.Append(string.Format("; {0}", ex.InnerException.Message));
                }

                Log.ErrorFormat("Error parsing configuration: {0}", msg.ToString());

                throw ex;
            }
        }

        /// <summary>
        /// Populates the properties on the configuration target with the values from the given config map.
        /// For any given property, the parser looks for a method named Set{PropertyName}.
        /// If this method exists the parser will invoke it to set the value.
        /// If it doesn't the parser will try the property setter.
        /// </summary>
        /// <param name="config"></param>
        public void ParseConfiguration(IDictionary<string, object> config)
        {
            foreach (KeyValuePair<string, ParseConfigurationValue> kvp in parsers)
            {
                object val;
                if (config.TryGetValue(kvp.Key, out val))
                {
                    try
                    {
                        try
                        {
                            kvp.Value.Invoke(val);
                        }
                        catch (ArgumentException ex)
                        {
                            if (ex.Message.Contains("cannot be converted"))
                            {
                                throw new InvalidCastException(ex.Message, ex);
                            }
                            throw;
                        }
                        catch (TargetInvocationException ex)
                        {
                            throw ex.InnerException;
                        }
                    }
                    catch (InvalidCastException ex)
                    {
                        throw new InvalidCastException(string.Format(
                            "Unable to cast configuration value \"{0}\".  The value was {1} ({2})",
                            kvp.Key, val, val.GetType()), ex);
                    }
                    catch (Exception ex)
                    {
                        if (val == null)
                        {
                            throw new ConfigurationParserException(string.Format(
                                "An error occurred parsing the configuration value \"{0}\".  The value was null",
                                kvp.Key), ex);
                        }
                        else
                        {
                            throw new ConfigurationParserException(string.Format(
                                "An error occurred parsing the configuration value \"{0}\".  The value was {1} ({2}).  Error : {3}",
                                kvp.Key, val, val.GetType(), ex.Message), ex);
                        }
                    }
                }
                else
                {
                    Log.FinestFormat("No configuration value for key \"{0}\"", kvp.Key);
                }
            }
        }

        // The JSON parser may deliver items of type Decimal,
        // rather than doing the conversion to Single or Double for us.
        public static float ToFloat(object value)
        {
            if (value is int)
            {
                return (float)(int)value;
            }
            else if (value is decimal) // Decimal values come back from the JSON parser
            {
                return decimal.ToSingle((decimal)value);
            }
            else if (value is float)
            {
                return (float)value;
            }
            else if (value is double)
            {
                return (float)(double)value;
            }
            return Convert.ToSingle("NaN");
        }

        class SetterParser
        {
            private readonly object configurationObject;
            private readonly MethodInfo method;

            public SetterParser(object configurationObject, MethodInfo method)
            {
                this.configurationObject = configurationObject;
                this.method = method;
            }

            public void Set(object val)
            {
                method.Invoke(configurationObject, new object[] { val });
            }

        }

        class PropertyParser
        {
            protected object ConfigurationObject { get; private set; }
            protected PropertyInfo PropertyInfo { get; private set; }

            public PropertyParser(object configurationObject, PropertyInfo prop)
            {
                this.ConfigurationObject = configurationObject;
                this.PropertyInfo = prop;
            }

            public void Set(object value)
            {
                // Watch out: The JSON parser may deliver ints, decimals, strings, lists or maps.
                // We have to convert decimals here to singles because SetValue will fail
                // to convert decimal to float for us.
                if (value is decimal) // Decimal values come back from the JSON parser
                {
                    value = decimal.ToSingle((decimal)value);
                }
                PropertyInfo.SetValue(ConfigurationObject, value, null);
            }
        }

        class CollectionPropertyParser : PropertyParser
        {
            public CollectionPropertyParser(object configurationObject, PropertyInfo prop) : base(configurationObject, prop)
            {
            }

            public void SetList(object value)
            {
                var list = value as System.Collections.ArrayList;

                Type t = PropertyInfo.PropertyType;
                object propList = PropertyInfo.GetValue(ConfigurationObject, null);
                MethodInfo m = t.GetMethod("Clear");
                m.Invoke(propList, new object[0]);
                m = t.GetMethod("Add");
                foreach (object val in list)
                {
                    m.Invoke(propList, new object[] { val });
                }

            }
        }

        /// <summary>
        /// Log the key/values of the ConfigurationAttribute properties of the given object.
        /// </summary>
        /// <param name="config"></param>
        public static void LogConfig(object config)
        {
            foreach (PropertyInfo prop in config.GetType().GetProperties())
            {
                object[] attr = prop.GetCustomAttributes(typeof(ConfigurationAttribute), false);
                if (attr.Length > 0)
                {
                    var configAttr = attr[0] as ConfigurationAttribute;
                    string key = configAttr.Key;
                    object val = prop.GetValue(config, null);
                    LogKeyValuePair(key, val);
                }
            }
        }

        private static void LogKeyValuePair(string key, object val)
        {
            var enumerable = val as IEnumerable;
            if (enumerable != null)
            {
                val = Strings.ToString(enumerable);
            }
            Log.DebugFormat("{0} = {1}", key, val);
        }

    }

    /// <summary>
    /// Thrown when there is some problem parsing the configuration.
    /// </summary>
    public class ConfigurationParserException : Exception
    {
        public ConfigurationParserException(string message)
            : base(message)
        {
        }
        public ConfigurationParserException(string message, Exception original)
            : base(message, original)
        {
        }
    }
}
