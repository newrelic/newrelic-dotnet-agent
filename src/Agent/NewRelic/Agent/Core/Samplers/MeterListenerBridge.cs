// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NewRelic.Agent.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace NewRelic.Agent.Core.Samplers
{
    public class MeterListenerBridge : IDisposable
    {
        private dynamic _meterListener;
        private static Meter NewRelicBridgeMeter = new Meter("NewRelicOTelBridgeMeter");
        private static ConcurrentDictionary<string, Meter> _bridgedMeters = new ConcurrentDictionary<string, Meter>();
        private static ConcurrentDictionary<Type, Func<Meter, string, string, string, object>> _createInstrumentDelegates = new ConcurrentDictionary<Type, Func<Meter, string, string, string, object>>();
        private MeterProvider _meterProvider;

        public MeterListenerBridge()
        {
            var providerBuilder = Sdk.CreateMeterProviderBuilder()
            //.ConfigureResource(r => r.AddService("myservice")) TODO: Add resource information
            .AddMeter("*")
            .AddConsoleExporter((exporterOptions, metricReaderOptions) =>
            {
                exporterOptions.Targets = ConsoleExporterOutputTargets.Console;

                metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000 * 5;
                metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
            });

            _meterProvider = providerBuilder.Build();
        }

        public void Start()
        {
            if (_meterListener == null)
            {
                try
                {
                    TryCreateMeterListener();
                }
                catch (Exception ex)
                {
                    Log.Warn(ex, "Failed to create MeterListener");
                }
                _meterListener?.Start();
            }
        }

        public void Stop()
        {
        }

        public void Dispose()
        {
            _meterListener?.Dispose();
            _meterProvider.Dispose();
        }

        private void TryCreateMeterListener()
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "System.Diagnostics.DiagnosticSource");
            var meterListenerType = assembly.GetType("System.Diagnostics.Metrics.MeterListener", throwOnError: false);
            var instrumentType = assembly.GetType("System.Diagnostics.Metrics.Instrument", throwOnError: false);
            if (meterListenerType != null && instrumentType != null)
            {
                _meterListener = Activator.CreateInstance(meterListenerType);

                var instrumentPublishProperty = meterListenerType.GetProperty("InstrumentPublished");

                var instrumentParameter = Expression.Parameter(instrumentType, "instrument");
                var listenerParameter = Expression.Parameter(meterListenerType, "listener");

                var meterNameProperty = Expression.Property(Expression.Property(instrumentParameter, "Meter"), "Name");

                var shouldEnableMethod = typeof(MeterListenerBridge).GetMethod(nameof(ShouldEnableInstrumentsInMeter), BindingFlags.NonPublic | BindingFlags.Static);
                var shouldEnableCall = Expression.Call(null, shouldEnableMethod, meterNameProperty);

                var getInstrumentStateMethod = typeof(MeterListenerBridge).GetMethod(nameof(GetStateForInstrument), BindingFlags.NonPublic | BindingFlags.Static);
                var getInstrumentStateCall = Expression.Call(null, getInstrumentStateMethod, instrumentParameter);

                var enableMeasurementEventsMethod = Expression.Call(listenerParameter, "EnableMeasurementEvents", null, instrumentParameter, getInstrumentStateCall);

                var lambdaBody = Expression.IfThen(shouldEnableCall, enableMeasurementEventsMethod);

                var lambda = Expression.Lambda(instrumentPublishProperty.PropertyType, lambdaBody, instrumentParameter, listenerParameter);

                instrumentPublishProperty.SetValue(_meterListener, lambda.Compile());


                SubscribeToMeasurementUpdates<int>(meterListenerType, instrumentType);

                SubscribeToMeasurementUpdates<double>(meterListenerType, instrumentType);
                SubscribeToMeasurementUpdates<float>(meterListenerType, instrumentType);
            }
        }

        private void SubscribeToMeasurementUpdates<T>(Type meterListenerType, Type instrumentType)
        {
            var setMeasurementEventCallbackMethodInfo = meterListenerType.GetMethod("SetMeasurementEventCallback").MakeGenericMethod(typeof(T));
            var callbackDelegateType = setMeasurementEventCallbackMethodInfo.GetParameters()[0].ParameterType;

            var instrumentParameter = Expression.Parameter(instrumentType, "instrument");
            var measurementParameter = Expression.Parameter(typeof(T), "measurement");
            var tagsParameter = Expression.Parameter(typeof(ReadOnlySpan<KeyValuePair<string, object>>), "tags");
            var stateParameter = Expression.Parameter(typeof(object), "state");

            var methodCall = Expression.Call(
                typeof(MeterListenerBridge),
                nameof(OnMeasurementRecorded),
                [typeof(T)],
                instrumentParameter,
                measurementParameter,
                tagsParameter,
                stateParameter);
            var measurementRecordedLambda = Expression.Lambda(callbackDelegateType, methodCall, instrumentParameter, measurementParameter, tagsParameter, stateParameter);

            setMeasurementEventCallbackMethodInfo.Invoke(_meterListener, new object[] { measurementRecordedLambda.Compile() });
        }


        private static bool ShouldEnableInstrumentsInMeter(string meterName)
        {
            // TODO: If the agent ever targets .net 9 the .net 9 build of the agent will not need to bridge the runtime metrics meter that is
            // built into the diagnostics source library. This check will need to be updated to not bridge the runtime metrics meter.
            return meterName == "MyTestMeter";
        }

        private static object GetStateForInstrument(object instrument)
        {
            dynamic dynamicInstrument = instrument;
            var instrumentTypeName = instrument.GetType().Name;
            var genericType = instrument.GetType().GetGenericArguments().FirstOrDefault();

            if (genericType == null || dynamicInstrument.IsObservable)
            {
                return null;
            }

            var meter = _bridgedMeters.GetOrAdd(dynamicInstrument.Meter.Name, (Func<string, Meter>)CreateBridgedMeterFromInstrument);
            var createInstrumentDelegate = _createInstrumentDelegates.GetOrAdd(instrument.GetType(), CreateBridgedInstrumentDelegate);

            if (createInstrumentDelegate == null)
            {
                return null;
            }

            // TODO: Tags are not avaiilable on older instruments, need a way to dynamically get them for newer versions
            return createInstrumentDelegate.Invoke(meter, dynamicInstrument.Name, dynamicInstrument.Unit, dynamicInstrument.Description);

            Meter CreateBridgedMeterFromInstrument(string _)
            {
                return CreateBridgedMeter(dynamicInstrument.Meter);
            }
        }

        private static Func<Meter, string, string, string, object> CreateBridgedInstrumentDelegate(Type originalInstrumentType)
        {
            var genericType = originalInstrumentType.GetGenericArguments().FirstOrDefault();
            if (genericType == null)
            {
                return null;
            }

            string createInstrumentMethodName = originalInstrumentType.Name switch
            {
                "Counter`1" => "CreateCounter",
                "Histogram`1" => "CreateHistogram",
                "UpDownCounter`1" => "CreateUpDownCounter",
                _ => null
            };

            var methodInfo = typeof(Meter).GetMethod(createInstrumentMethodName, [typeof(string), typeof(string), typeof(string)]).MakeGenericMethod(genericType);
            var createDelegate = (Func<Meter, string, string, string, object>)methodInfo.CreateDelegate(typeof(Func<Meter, string, string, string, object>));

            return createDelegate;
        }

        private static Meter CreateBridgedMeter(object meter)
        {
            dynamic dynamicMeter = meter;

            // TODO: Tags and Scope are not available on older meters, need a way to dynamically get them for newer versions
            return new Meter(dynamicMeter.Name, dynamicMeter.Version);
        }

        private static void OnMeasurementRecorded<T>(object instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object>> tags, object state)
            where T : struct
        {
            dynamic dynamicInstrument = instrument;
            Console.WriteLine($"{dynamicInstrument.Name} recorded measurement {measurement}");

            if (state == null)
            {
                return;
            }

            var stateTypeName = state.GetType().Name;
            if (state is Counter<T> counter)
            {
                counter.Add(measurement, tags);
                Console.WriteLine($"Signaled {measurement} to {counter.Meter.Name} - {counter.Name}");
            }
        }
    }
}
