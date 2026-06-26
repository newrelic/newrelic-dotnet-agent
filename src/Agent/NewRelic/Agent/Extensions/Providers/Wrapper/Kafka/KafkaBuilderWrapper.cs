// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Kafka;

public class KafkaBuilderWrapper : IWrapper
{
    private ConcurrentDictionary<Type, Func<object, IEnumerable>> _builderConfigGetterDictionary = new();

    private const string WrapperName = "KafkaBuilderWrapper";
    private const string BootstrapServersKey = "bootstrap.servers";

    public bool IsTransactionRequired => false;
    public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
    {
        return new CanWrapResponse(WrapperName.Equals(instrumentedMethodInfo.RequestedWrapperName));
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        var builder = instrumentedMethodCall.MethodCall.InvocationTarget;

        if (!_builderConfigGetterDictionary.TryGetValue(builder.GetType(), out var configGetter))
        {
            configGetter = VisibilityBypasser.Instance.GeneratePropertyAccessor<IEnumerable>(builder.GetType(), "Config");
            _builderConfigGetterDictionary[builder.GetType()] = configGetter;
        }

        dynamic configuration = configGetter(builder);

        string bootstrapServers = null;
        var fullConfig = new Dictionary<string, string>();

        foreach (var kvp in configuration)
        {
            var key = kvp.Key as string;
            var value = kvp.Value as string;
            if (key != null && value != null)
                fullConfig[key] = value;
            if (key == BootstrapServersKey)
                bootstrapServers = value;
        }

        if (!string.IsNullOrEmpty(bootstrapServers))
            return Delegates.GetDelegateFor<object>(onSuccess: (builtObject) =>
            {
                KafkaHelper.AddBootstrapServersToCache(builtObject, bootstrapServers);
                KafkaHelper.ScheduleClusterIdFetch(builtObject, bootstrapServers, fullConfig);
            });

        return Delegates.NoOp;
    }
}
