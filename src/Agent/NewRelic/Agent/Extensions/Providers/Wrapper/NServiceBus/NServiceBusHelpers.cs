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
        private static Func<object, Dictionary<string, string>> _getHeadersSendMessageFunc;
        private static Func<object, Dictionary<string, string>> _getHeadersPipelineFunc;
        private static Func<object, Dictionary<string, string>> _getHeadersReceiveMessageFunc;

        private static Func<object, object> _getIncomingLogicalMessageFunc;

        private static Func<object, object> _getMessageFromIncomingLogicalMessageContextFunc;
        private static Func<object, Dictionary<string, string>> _getHeadersFromIncomingLogicalMessageContextFunc;

        private static Func<object, object> _getMessageFromOutgoingSendContextFunc;
        private static Func<object, object> _getMessageFromOutgoingPublishContextFunc;

        public const string OutgoingSendContextTypeName = "NServiceBus.OutgoingSendContext";
        public const string OutgoingPublishContextTypeName = "NServiceBus.OutgoingPublishContext";


        private static Func<object, Type> _getMessageTypeSendMessageFunc;
        private static Func<object, Type> _getMessageTypePipelineFunc;
        private static Func<object, Type> _getMessageTypeReceiveMessageFunc;
        private static Func<object, Type> _getMessageTypeLoadHandlersConnectorFunc;

        #region Wrapper Specific Helpers - does not depend on other bypasser results

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

        public static Dictionary<string, string> GetHeadersReceiveMessage(object logicalMessage)
        {
            var getHeadersReceiveMessageFunc = _getHeadersReceiveMessageFunc ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<Dictionary<string, string>>(logicalMessage.GetType(), "Headers");
            return getHeadersReceiveMessageFunc(logicalMessage);
        }

        #endregion

        // receive - load; no bypaser in here
        public static void ProcessHeaders(Dictionary<string, string> headers, IAgent agent)
        {
            if (headers == null)
            {
                return;
            }

            agent.CurrentTransaction.AcceptDistributedTraceHeaders(headers, GetHeaderValue, TransportType.HTTP);

            static IEnumerable<string> GetHeaderValue(Dictionary<string, string> carrier, string key)
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

        public static void CreateOutboundHeadersSendMessage(IAgent agent, object logicalMessage)
        {
            var getHeadersSendMessageFunc = _getHeadersSendMessageFunc ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<Dictionary<string, string>>(logicalMessage.GetType(), "Headers");
            CreateOutboundHeaders(agent, logicalMessage, getHeadersSendMessageFunc);
        }

        public static void CreateOutboundHeadersPipeline(IAgent agent, object logicalMessage)
        {
            var getHeadersPipelineFunc = _getHeadersPipelineFunc ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<Dictionary<string, string>>(logicalMessage.GetType(), "Headers");
            CreateOutboundHeaders(agent, logicalMessage, getHeadersPipelineFunc);
        }

        private static void CreateOutboundHeaders(IAgent agent, object logicalMessage, Func<object, Dictionary<string, string>> getHeaders)
        {
            // We don't need to check if headers are null since we will create the headers object if its null
            // create action for use later
            var setHeaders = new Action<object, string, string>((carrier, key, value) =>
            {
                var headers = getHeaders(logicalMessage);
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

            static void SetHeaders(object logicalMessage, Dictionary<string, string> headers)
            {
                // Unlike the GetHeaders function, we can't cache this action.  It is only valid for the specific logicalMessage object instance provided.
                var action = VisibilityBypasser.Instance.GeneratePropertySetter<Dictionary<string, string>>(logicalMessage, "Headers");
                action(headers);
            }
        }

        /// <summary>
        /// Returns a metric name based on the type of message. The source/destination queue isn't always known (depending on the circumstances) and in some cases isn't even relevant. The message type is always known and is always relevant.
        /// </summary>
        /// <param name="logicalMessage"></param>
        /// <returns></returns>
        public static string TryGetQueueNameSendMessage(object logicalMessage)
        {
            var getMessageTypeFunc = _getMessageTypeSendMessageFunc ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<Type>(logicalMessage.GetType(), "MessageType");
            return TryGetQueueName(logicalMessage, getMessageTypeFunc);
        }

        /// <summary>
        /// Returns a metric name based on the type of message. The source/destination queue isn't always known (depending on the circumstances) and in some cases isn't even relevant. The message type is always known and is always relevant.
        /// </summary>
        /// <param name="logicalMessage"></param>
        /// <returns></returns>
        public static string TryGetQueueNamePipeline(object logicalMessage)
        {
            var getMessageTypeFunc = _getMessageTypePipelineFunc ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<Type>(logicalMessage.GetType(), "MessageType");
            return TryGetQueueName(logicalMessage, getMessageTypeFunc);
        }

        /// <summary>
        /// Returns a metric name based on the type of message. The source/destination queue isn't always known (depending on the circumstances) and in some cases isn't even relevant. The message type is always known and is always relevant.
        /// </summary>
        /// <param name="logicalMessage"></param>
        /// <returns></returns>
        public static string TryGetQueueNameReceiveMessage(object logicalMessage)
        {
            var getMessageTypeFunc = _getMessageTypeReceiveMessageFunc ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<Type>(logicalMessage.GetType(), "MessageType");
            return TryGetQueueName(logicalMessage, getMessageTypeFunc);
        }

        /// <summary>
        /// Returns a metric name based on the type of message. The source/destination queue isn't always known (depending on the circumstances) and in some cases isn't even relevant. The message type is always known and is always relevant.
        /// </summary>
        /// <param name="logicalMessage"></param>
        /// <returns></returns>
        public static string TryGetQueueNameLoadHandlersConnector(object logicalMessage)
        {
            var getMessageTypeFunc = _getMessageTypeLoadHandlersConnectorFunc ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<Type>(logicalMessage.GetType(), "MessageType");
            return TryGetQueueName(logicalMessage, getMessageTypeFunc);
        }

        private static string TryGetQueueName(object logicalMessage, Func<object, Type> getMessageType)
        {
            var messageType = getMessageType(logicalMessage);

            if (messageType == null)
            {
                return null;
            }

            return messageType.FullName;
        }
    }
}
