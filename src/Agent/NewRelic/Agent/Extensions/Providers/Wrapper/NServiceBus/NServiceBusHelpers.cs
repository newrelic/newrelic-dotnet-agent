// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.NServiceBus
{
    public class NServiceBusHelpers
    {
        private static Func<object, Dictionary<string, string>> _getHeadersFunc;
        private static Func<object, object> _getIncomingLogicalMessageFunc;

        private static Func<object, object> _getMessageFromIncomingLogicalMessageContextFunc;
        private static Func<object, Dictionary<string, string>> _getHeadersFromIncomingLogicalMessageContextFunc;

        private static Func<object, object> _getMessageFromOutgoingSendContextFunc;
        private static Func<object, object> _getMessageFromOutgoingPublishContextFunc;

        public const string OutgoingSendContextTypeName = "NServiceBus.OutgoingSendContext";
        public const string OutgoingPublishContextTypeName = "NServiceBus.OutgoingPublishContext";


        private static Func<object, Type> _getMessageTypeFunc;

        public static object GetMessageFromOutgoingContext(object outgoingContext)
        {
            if (outgoingContext.GetType().FullName == OutgoingSendContextTypeName)
            {
                var getMessageFromOutgoingSendContextFunc = _getMessageFromOutgoingSendContextFunc ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(outgoingContext.GetType(), "Message");
                return getMessageFromOutgoingSendContextFunc(outgoingContext);
            }
            else if (outgoingContext.GetType().FullName == OutgoingPublishContextTypeName)
            {
                var getMessageFromOutgoingPublishContextFunc = _getMessageFromOutgoingPublishContextFunc ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(outgoingContext.GetType(), "Message");
                return getMessageFromOutgoingPublishContextFunc(outgoingContext);
            }

            return null;
        }

        public static object GetMessageFromIncomingLogicalMessageContext(object incomingLogicalMessageContext)
        {
            var getLogicalMessageContextFunc = _getMessageFromIncomingLogicalMessageContextFunc ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(incomingLogicalMessageContext.GetType(), "Message");
            return getLogicalMessageContextFunc(incomingLogicalMessageContext);
        }

        public static Dictionary<string, string> GetHeadersFromIncomingLogicalMessageContext(object incomingLogicalMessageContext)
        {
            var getHeadersFromIncomingLogicalMessageContextFunc = _getHeadersFromIncomingLogicalMessageContextFunc ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<Dictionary<string, string>>(incomingLogicalMessageContext.GetType(), "Headers");
            return getHeadersFromIncomingLogicalMessageContextFunc(incomingLogicalMessageContext);
        }

        public static object GetIncomingLogicalMessage(object incomingContext)
        {
            var getLogicalMessageFunc = _getIncomingLogicalMessageFunc ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(incomingContext.GetType(), "IncomingLogicalMessage");
            return getLogicalMessageFunc(incomingContext);
        }

        public static Dictionary<string, string> GetHeaders(object logicalMessage)
        {
            var getHeadersFunc = _getHeadersFunc ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<Dictionary<string, string>>(logicalMessage.GetType(), "Headers");
            return getHeadersFunc(logicalMessage);
        }

        public static void SetHeaders(object logicalMessage, Dictionary<string, string> headers)
        {
            // Unlike the GetHeaders function, we can't cache this action.  It is only valid for the specific logicalMessage object instance provided.
            var action = VisibilityBypasser.Instance.GeneratePropertySetter<Dictionary<string, string>>(logicalMessage, "Headers");

            action(headers);
        }

        /// <summary>
        /// Returns a metric name based on the type of message. The source/destination queue isn't always known (depending on the circumstances) and in some cases isn't even relevant. The message type is always known and is always relevant.
        /// </summary>
        /// <param name="logicalMessage"></param>
        /// <returns></returns>
        public static string TryGetQueueName(object logicalMessage)
        {
            var getMessageTypeFunc = _getMessageTypeFunc ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<Type>(logicalMessage.GetType(), "MessageType");

            var messageType = getMessageTypeFunc(logicalMessage);

            if (messageType == null)
            {
                return null;
            }

            return messageType.FullName;
        }

        public static void ProcessHeaders(Dictionary<string, string> headers, IAgent agent)
        {
            agent.CurrentTransaction.AcceptDistributedTraceHeaders(headers, GetHeaderValue, TransportType.HTTP);
        }

        public static void CreateOutboundHeaders(IAgent agent, object logicalMessage)
        {
            // We need headers to attach CAT/DT payload, bail out if headers are null,
            var headers = GetHeaders(logicalMessage);
            if (headers == null)
            {
                return;
            }

            var setHeaders = new Action<object, string, string>((carrier, key, value) =>
            {
                var headers = GetHeaders(carrier);

                if (headers == null)
                {
                    headers = new Dictionary<string, string>();
                    SetHeaders(carrier, headers);
                }
                else if (headers is IReadOnlyDictionary<string, object>)
                {
                    headers = new Dictionary<string, string>(headers);
                    SetHeaders(carrier, headers);
                }

                headers[key] = value;
            });

            agent.CurrentTransaction.InsertDistributedTraceHeaders(logicalMessage, setHeaders);
        }

        private static IEnumerable<string> GetHeaderValue(Dictionary<string, string> carrier, string key)
        {
            if (carrier != null)
            {
                foreach (var item in carrier)
                {
                    if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        return new string[] { item.Value };
                    }
                }
            }
            return null;
        }

    }
}
