// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NewRelic.Agent.Core.Logging;
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
        private static ConcurrentDictionary<Type, object> _createInstrumentDelegates = new ConcurrentDictionary<Type, object>();
        private static ConcurrentDictionary<Type, object> _bridgeMeasurementDelegates = new ConcurrentDictionary<Type, object>();
        private static ConcurrentDictionary<Type, ObservableInstrumentCacheData> _createObservableInstrumentCache = new ConcurrentDictionary<Type, ObservableInstrumentCacheData>();

        private OpenTelemetrySDKLogger _sdkLogger;
        private MeterProvider _meterProvider;

        public MeterListenerBridge()
        {
            _sdkLogger = new OpenTelemetrySDKLogger();

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
            _sdkLogger.Dispose();
        }

        private void TryCreateMeterListener()
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "System.Diagnostics.DiagnosticSource");
            var meterListenerType = assembly.GetType("System.Diagnostics.Metrics.MeterListener", throwOnError: false);
            var instrumentType = assembly.GetType("System.Diagnostics.Metrics.Instrument", throwOnError: false);
            if (meterListenerType != null && instrumentType != null)
            {
                _meterListener = Activator.CreateInstance(meterListenerType);

                SubscribeToInstrumentPublishedEvent(meterListenerType, instrumentType);

                SubscribeToMeasurementUpdates<byte>(meterListenerType, instrumentType);
                SubscribeToMeasurementUpdates<short>(meterListenerType, instrumentType);
                SubscribeToMeasurementUpdates<int>(meterListenerType, instrumentType);
                SubscribeToMeasurementUpdates<long>(meterListenerType, instrumentType);
                SubscribeToMeasurementUpdates<float>(meterListenerType, instrumentType);
                SubscribeToMeasurementUpdates<double>(meterListenerType, instrumentType);
                SubscribeToMeasurementUpdates<decimal>(meterListenerType, instrumentType);

                SubscribeToMeasurementCompletedEvent(meterListenerType, instrumentType);
            }
        }

        private void SubscribeToInstrumentPublishedEvent(Type meterListenerType, Type instrumentType)
        {
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

        private void SubscribeToMeasurementCompletedEvent(Type meterListenerType, Type instrumentType)
        {
            var measurementsCompletedProperty = meterListenerType.GetProperty("MeasurementsCompleted");

            var instrumentParameter = Expression.Parameter(instrumentType, "instrument");
            var stateParameter = Expression.Parameter(typeof(object), "state");

            var disableBridgedInstrumentMethod = typeof(MeterListenerBridge).GetMethod(nameof(DisableBridgedInstrument), BindingFlags.NonPublic | BindingFlags.Static);
            var disableBridgedInstrumentCall = Expression.Call(null, disableBridgedInstrumentMethod, stateParameter);

            var measurmentsCompletedLambda = Expression.Lambda(measurementsCompletedProperty.PropertyType, disableBridgedInstrumentCall, instrumentParameter, stateParameter);

            measurementsCompletedProperty.SetValue(_meterListener, measurmentsCompletedLambda.Compile());
        }

        private static void DisableBridgedInstrument(object state)
        {
            if (state == null)
            {
                return;
            }

            var bridgedInstrument = state as Instrument;
            if (bridgedInstrument == null)
            {
                return;
            }

            // The MeasurementsCompleted event is triggered when an instrument is no longer being subscribed to by a MeterListener
            // or the Meter holding the instrumented is Disposed. In our use case, we subscribe to all instruments in a meter and
            // do not disable individual individual instruments, so the only way this event will be triggered is if the corresponding
            // meter is no longer being used by the application or if our MeterListener is being Disposed. In either case we can
            // just Dispose of the bridged Meter.

            if (_bridgedMeters.TryRemove(bridgedInstrument.Meter.Name, out var bridgedMeter))
            {
                var meterName = bridgedMeter.Name;
                bridgedMeter.Dispose();
                Console.WriteLine("Disposed bridged meter: " + meterName);
            }
        }


        private static bool ShouldEnableInstrumentsInMeter(string meterName)
        {
            // TODO: If the agent ever targets .net 9 the .net 9 build of the agent will not need to bridge the runtime metrics meter that is
            // built into the diagnostics source library. This check will need to be updated to not bridge the runtime metrics meter.
            return true;
        }

        private static object GetStateForInstrument(object instrument)
        {
            dynamic dynamicInstrument = instrument;
            var instrumentType = instrument.GetType();
            var genericType = instrumentType.GetGenericArguments().FirstOrDefault();

            if (genericType == null)
            {
                return null;
            }

            Meter meter = _bridgedMeters.GetOrAdd(dynamicInstrument.Meter.Name, (Func<string, Meter>)CreateBridgedMeterFromInstrument);

            if (dynamicInstrument.IsObservable)
            {
                var cacheData = _createObservableInstrumentCache.GetOrAdd(instrumentType, GetObservableInstrumentCacheData);
                if (cacheData == null)
                {
                    return null;
                }

                var createCallbackAndObservableInstrument = cacheData.CreateCallbackAndObservableInstrumentDelegate;
                return createCallbackAndObservableInstrument(instrument, meter, cacheData);
            }

            var createInstrumentDelegate = (Func<Meter, string, string, string, object>)_createInstrumentDelegates.GetOrAdd(instrumentType, CreateBridgedInstrumentDelegate);

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

        private static ObservableInstrumentCacheData GetObservableInstrumentCacheData(Type instrumentType)
        {
            var genericType = instrumentType.GetGenericArguments().FirstOrDefault();
            if (genericType == null)
            {
                return null;
            }

            var cacheData = new ObservableInstrumentCacheData();
            cacheData.CreateObservableInstrumentDelegate = CreateBridgedObservableInstrumentDelegate(instrumentType);
            cacheData.CreateCallbackAndObservableInstrumentDelegate = CreateCallbackAndBridgedObservableInstrumentAction(instrumentType);
            cacheData.ObserveMethodDelegate = CreateObserveMethodInvoker(instrumentType);

            return cacheData;
        }

        public static Func<object, IEnumerable> CreateObserveMethodInvoker(Type instrumentType)
        {
            var observeMethod = instrumentType.GetMethod("Observe", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            var instrumentParameter = Expression.Parameter(typeof(object), "instrument");
            var typedInstrument = Expression.Convert(instrumentParameter, instrumentType);
            var observeMethodCall = Expression.Call(typedInstrument, observeMethod);
            var convertedResult = Expression.Convert(observeMethodCall, typeof(IEnumerable));
            var lambda = Expression.Lambda<Func<object, IEnumerable>>(convertedResult, instrumentParameter);

            return lambda.Compile();
        }

        private static Func<object, Meter, ObservableInstrumentCacheData, Instrument> CreateCallbackAndBridgedObservableInstrumentAction(Type instrumentType)
        {
            var genericType = instrumentType.GetGenericArguments().FirstOrDefault();
            var methodInfo = typeof(MeterListenerBridge).GetMethod(nameof(CreateCallbackAndBridgedObservableInstrument), BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(genericType);
            return (Func<object, Meter, ObservableInstrumentCacheData, Instrument>)methodInfo.CreateDelegate(typeof(Func<object, Meter, ObservableInstrumentCacheData, Instrument>));
        }

        private static Instrument CreateCallbackAndBridgedObservableInstrument<T>(object instrument, Meter meter, ObservableInstrumentCacheData cacheData) where T : struct
        {
            var createObservableInstrument = (CreateObservableInstrumentDelegate<T>)cacheData.CreateObservableInstrumentDelegate;
            if (createObservableInstrument == null)
            {
                return null;
            }

            dynamic dynamicInstrument = instrument;

            // Need to create a function that can call the Observe() method on the instrument (the protected parameterless method)
            // and transform the IEnumerable<Measurement<T>> into the bridged IEnumerable<Measurement<T>> that the ilrepacked code expects

            var observeMethod = cacheData.ObserveMethodDelegate;
            return createObservableInstrument(meter, (string)dynamicInstrument.Name, ForwardObservedMeasurements, (string)dynamicInstrument.Unit, (string)dynamicInstrument.Description);

            IEnumerable<Measurement<T>> ForwardObservedMeasurements()
            {
                return BridgeMeasurements<T>(observeMethod(instrument));
            }
        }

        private static IEnumerable<Measurement<T>> BridgeMeasurements<T>(IEnumerable originalMeasurements) where T : struct
        {
            var bridgedMeasurements = new List<Measurement<T>>();
            Func<object, Measurement<T>> createBridgedMeasurement = null;
            foreach (object measurement in originalMeasurements)
            {
                // Initializing the delegate to create the bridged measurment within the loop because the type of the original measurements
                // is not IEnumerable<Measurement<T>> but is the concrete collection instead (which may change in the future).
                if (createBridgedMeasurement == null)
                {
                    createBridgedMeasurement = (Func<object, Measurement<T>>)_bridgeMeasurementDelegates.GetOrAdd(measurement.GetType(), GetMethodToBridgeMeasurement<T>);
                }
                bridgedMeasurements.Add(createBridgedMeasurement(measurement));
            }

            return bridgedMeasurements;
        }

        private static Func<object, Measurement<T>> GetMethodToBridgeMeasurement<T>(Type originalMeasurementType) where T : struct
        {
            var measurementConstructor = typeof(Measurement<T>).GetConstructor([typeof(T), typeof(ReadOnlySpan<KeyValuePair<string, object>>)]);
            if (measurementConstructor == null)
            {
                return null;
            }

            var originalMeasurementParameter =  Expression.Parameter(typeof(object), "originalMeasurement");
            var typedOriginalMeasurement = Expression.Convert(originalMeasurementParameter, originalMeasurementType);

            var valueProperty = originalMeasurementType.GetProperty("Value");
            var tagsProperty = originalMeasurementType.GetProperty("Tags");

            var valuePropertyAccess = Expression.Property(typedOriginalMeasurement, originalMeasurementType, "Value");
            var tagsPropertyAccess = Expression.Property(typedOriginalMeasurement, originalMeasurementType, "Tags");

            var newMeasurement = Expression.New(measurementConstructor, valuePropertyAccess, tagsPropertyAccess);
            var lambda = Expression.Lambda<Func<object, Measurement<T>>>(newMeasurement, originalMeasurementParameter);

            return lambda.Compile();
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
                // TODO: Add gauge support with .net 9 (needs corresponding otel sdk update)
                _ => null
            };

            if (createInstrumentMethodName == null)
            {
                return null;
            }

            var methodInfo = typeof(Meter).GetMethod(createInstrumentMethodName, [typeof(string), typeof(string), typeof(string)]).MakeGenericMethod(genericType);
            var createDelegate = (Func<Meter, string, string, string, object>)methodInfo.CreateDelegate(typeof(Func<Meter, string, string, string, object>));

            return createDelegate;
        }

        private static Delegate CreateBridgedObservableInstrumentDelegate(Type originalInstrumentType)
        {
            var genericType = originalInstrumentType.GetGenericArguments().FirstOrDefault();
            if (genericType == null)
            {
                return null;
            }

            string createInstrumentMethodName = originalInstrumentType.Name switch
            {
                "ObservableCounter`1" => nameof(CreateBridgedObservableCounter),
                "ObservableGauge`1" => nameof(CreateBridgedObservableGauge),
                "ObservableUpDownCounter`1" => nameof(CreateBridgedObservableUpDownCounter),
                _ => null
            };

            if (createInstrumentMethodName == null)
            {
                return null;
            }

            var delegateType = typeof(CreateObservableInstrumentDelegate<>).MakeGenericType(genericType);
            var methodInfo = typeof(MeterListenerBridge).GetMethod(createInstrumentMethodName, BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(genericType);
            var createDelegate = methodInfo.CreateDelegate(delegateType);

            return createDelegate;
        }

        private delegate Instrument CreateObservableInstrumentDelegate<T>(Meter meter, string name, Func<IEnumerable<Measurement<T>>> callback, string unit, string description) where T : struct;

        private static Instrument CreateBridgedObservableCounter<T>(Meter meter, string name, Func<IEnumerable<Measurement<T>>> callback, string unit, string description) where T : struct
        {
            return meter.CreateObservableCounter(name, callback, unit, description);
        }

        private static Instrument CreateBridgedObservableGauge<T>(Meter meter, string name, Func<IEnumerable<Measurement<T>>> callback, string unit, string description) where T : struct
        {
            return meter.CreateObservableGauge(name, callback, unit, description);
        }

        private static Instrument CreateBridgedObservableUpDownCounter<T>(Meter meter, string name, Func<IEnumerable<Measurement<T>>> callback, string unit, string description) where T : struct
        {
            return meter.CreateObservableUpDownCounter(name, callback, unit, description);
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

            switch (state)
            {
                case Counter<T> counter:
                    counter.Add(measurement, tags);
                    Console.WriteLine($"Signaled {measurement} to {counter.Meter.Name} - {counter.Name}");
                    break;
                case Histogram<T> histogram:
                    histogram.Record(measurement, tags);
                    Console.WriteLine($"Signaled {measurement} to {histogram.Meter.Name} - {histogram.Name}");
                    break;
                case UpDownCounter<T> upDownCounter:
                    upDownCounter.Add(measurement, tags);
                    Console.WriteLine($"Signaled {measurement} to {upDownCounter.Meter.Name} - {upDownCounter.Name}");
                    break;
                // TODO: Add gauge support with .net 9 (needs corresponding otel sdk update)
                default:
                    break;
            }
        }

        public class ObservableInstrumentCacheData
        {
            public Delegate CreateObservableInstrumentDelegate { get; set; }
            public Func<object, Meter, ObservableInstrumentCacheData, Instrument> CreateCallbackAndObservableInstrumentDelegate { get; set; }
            public Func<object, IEnumerable> ObserveMethodDelegate { get; set; }
        }
    }
}